<?php

/**
 * Génère les métadonnées GLPI embarquées par GlpiNg pour reproduire ses API REST :
 *
 *   php generate.php <archive GLPI décompressée> <src/GlpiNg.Web/Api/Glpi/Metadata>
 *
 * L'archive est celle d'une version publiée (https://github.com/glpi-project/glpi/releases),
 * avec son dossier vendor/. Aucune base de données n'est nécessaire (voir boot.php). Ce qui
 * est produit n'est pas réécrit à la main : c'est la sortie du code de GLPI lui-même.
 *
 *  - tables.json.gz      colonnes de chaque table (glpi-empty.sql), dans l'ordre de GLPI
 *  - itemtypes.json.gz   par itemtype : table, liens HATEOAS, options de recherche (brutes et
 *                        telles que listSearchOptions les rend)
 *  - hlapi.json.gz       schémas de l'API v2 par version, AVEC leurs métadonnées x-field /
 *                        x-join / x-itemtype, que la documentation OpenAPI publiée retire
 *  - openapi-2.x.json.gz documentation OpenAPI de chaque version de l'API v2 (servie telle quelle)
 *  - defaults.json.gz    configuration « sûre » par défaut, profils et droits d'une installation
 *                        neuve, préférences d'affichage par défaut
 */

require __DIR__ . '/boot.php';

$out_dir = $argv[2] ?? '';
if ($out_dir === '' || !is_dir($out_dir)) {
    fwrite(STDERR, "Dossier de sortie manquant ou inexistant.\n");
    exit(1);
}

function glping_write(string $dir, string $name, mixed $data): void
{
    $json = json_encode($data, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE | JSON_PARTIAL_OUTPUT_ON_ERROR | JSON_PRESERVE_ZERO_FRACTION);
    file_put_contents("$dir/$name.gz", gzencode($json, 9));
    fwrite(STDERR, sprintf("%-22s %8d Ko (%d Ko compressé)\n", $name, strlen($json) / 1024, filesize("$dir/$name.gz") / 1024));
}

// --- Tables -------------------------------------------------------------------------------
$tables = [];
foreach ($GLOBALS['GLPING_SCHEMA'] as $table => $cols) {
    $tables[$table] = array_values(array_map(static fn($c) => [
        $c['Field'], $c['Type'], $c['Null'] === 'YES', $c['Default'], $c['has_default'], $c['auto_increment'],
    ], $cols));
}
glping_write($out_dir, 'tables.json', $tables);

// --- Itemtypes et options de recherche ----------------------------------------------------
$api = (new ReflectionClass(Glpi\Api\APIRest::class))->newInstanceWithoutConstructor();
$list = new ReflectionMethod(Glpi\Api\API::class, 'listSearchOptions');

$itemtypes = ['AllAssets'];
foreach (array_keys($tables) as $table) {
    $itemtype = getItemTypeForTable($table);
    if ($itemtype === null || !class_exists($itemtype)) {
        continue;
    }
    $rc = new ReflectionClass($itemtype);
    if ($rc->isAbstract() || !$rc->isSubclassOf(CommonDBTM::class)) {
        continue;
    }
    $itemtypes[] = $itemtype;
}

// Champs d'une option de recherche utiles à son évaluation (le reste est de l'affichage).
$raw_keys = ['table', 'field', 'linkfield', 'datatype', 'joinparams', 'searchequalsonfield', 'forcegroupby',
    'computation', 'nosearch', 'nodisplay', 'unit', 'max', 'min', 'step', 'toadd', 'searchtype', 'condition', 'itemtype_list'];

$items = [];
$errors = 0;
foreach ($itemtypes as $itemtype) {
    $entry = [];
    try {
        $entry['table'] = $itemtype === 'AllAssets' ? null : getTableForItemType($itemtype);
        $entry['hateoas'] = $itemtype === 'AllAssets' ? [] : Glpi\Api\API::getHatoasClasses($itemtype);
        if ($itemtype !== 'AllAssets') {
            $item = new $itemtype();
            $entry['entity_assign'] = $item->isEntityAssign();
            $entry['maybe_recursive'] = $item->maybeRecursive();
            $entry['maybe_deleted'] = $item->maybeDeleted();
            $entry['maybe_template'] = $item->maybeTemplate();
            $entry['type_name'] = $itemtype::getTypeName(1);
            $entry['fk'] = $itemtype::getForeignKeyField();
            $entry['is_itil'] = is_a($itemtype, CommonITILObject::class, true);
            $entry['is_tree'] = is_a($itemtype, CommonTreeDropdown::class, true);
            $entry['name_field'] = $itemtype::getNameField();
        }
        // Colonnes affichées par défaut dans une recherche (SearchOption::getDefaultToView), sans
        // la colonne Entité : celle-ci dépend du nombre d'entités, connu seulement à l'exécution.
        $_SESSION['glpi_multientitiesmode'] = 0;
        $entry['toview'] = array_values(Glpi\Search\SearchOption::getDefaultToView($itemtype, []));
        $entry['entity_opt'] = $itemtype === 'AllAssets' ? 80 : Glpi\Search\SearchOption::getOptionNumber($itemtype, 'completename', 'Entity');
        $entry['id_opt'] = $itemtype === 'AllAssets' ? 0 : Glpi\Search\SearchOption::getOptionNumber($itemtype, 'id');
    } catch (Throwable $e) {
        $entry['error_meta'] = $e->getMessage();
    }
    try {
        $entry['searchoptions'] = $list->invoke($api, $itemtype, [], false);
        $raw = [];
        foreach (Search::getOptions($itemtype) as $id => $opt) {
            if (!is_int($id) || !is_array($opt)) {
                continue;
            }
            $raw[$id] = array_intersect_key($opt, array_flip($raw_keys));
        }
        $entry['raw'] = $raw;
    } catch (Throwable $e) {
        $entry['error_so'] = get_class($e) . ': ' . $e->getMessage();
        $errors++;
    }
    $items[$itemtype] = $entry;
}
fwrite(STDERR, count($items) . " itemtypes, $errors sans options de recherche\n");
glping_write($out_dir, 'itemtypes.json', $items);

// --- API v2 : schémas avec métadonnées, et documentation OpenAPI ---------------------------
$versions = array_values(array_filter(
    array_column(Glpi\Api\HL\Router::getAPIVersions(), 'version'),
    static fn($v) => str_starts_with($v, '2.')
));

$router = Glpi\Api\HL\Router::getInstance();
$hl = [];
foreach ($versions as $version) {
    $schemas = [];
    foreach ($router->getControllers() as $controller) {
        $short = (new ReflectionClass($controller))->getShortName();
        try {
            $known = $controller::getKnownSchemas($version);
        } catch (Throwable $e) {
            fwrite(STDERR, "$version $short : " . $e->getMessage() . "\n");
            continue;
        }
        foreach ($known as $name => $schema) {
            $schema['x-controller'] = $short;
            // Même règle de nommage que OpenAPIGenerator::getComponentSchemas en cas de doublon.
            if (isset($schemas[$name])) {
                $other = $schemas[$name];
                unset($schemas[$name]);
                $schemas[str_replace('Controller', '', $other['x-controller']) . ' - ' . $name] = $other;
                $name = str_replace('Controller', '', $short) . ' - ' . $name;
            }
            $schemas[$name] = $schema;
        }
    }
    $hl[$version] = $schemas;

    $generator = new Glpi\Api\HL\OpenAPIGenerator($router, $version);
    glping_write($out_dir, "openapi-$version.json", $generator->getSchema());
    Glpi\Api\HL\OpenAPIGenerator::clearComponentSchemasCache();
}
glping_write($out_dir, 'hlapi.json', ['versions' => Glpi\Api\HL\Router::getAPIVersions(), 'schemas' => $hl]);

// --- Documentation de l'API v1, servie à sa racine ------------------------------------------
file_put_contents("$out_dir/apirest.md.gz", gzencode(file_get_contents(GLPING_GLPI_ROOT . '/apirest.md'), 9));

// --- Valeurs par défaut -------------------------------------------------------------------
$empty = $GLOBALS['GLPING_EMPTY_DATA'];
$rights = [];
foreach ($empty['glpi_profilerights'] as $row) {
    $rights[$row['profiles_id']][$row['name']] = (int) $row['rights'];
}
$display = [];
foreach ($empty['glpi_displaypreferences'] as $row) {
    if (($row['users_id'] ?? 0) == 0 && ($row['interface'] ?? 'central') === 'central') {
        $display[$row['itemtype']][(int) $row['rank']] = (int) $row['num'];
    }
}
foreach ($display as &$cols) {
    ksort($cols);
    $cols = array_values($cols);
}
unset($cols);

glping_write($out_dir, 'defaults.json', [
    'glpi_version' => GLPI_VERSION,
    'safe_config' => Config::getSafeConfig(true),
    'profiles' => $empty['glpi_profiles'],
    'profilerights' => $rights,
    'displaypreferences' => $display,
    'massive_actions' => [
        'MassiveAction:update' => __('Update'),
        'MassiveAction:clone' => __('Clone'),
        'MassiveAction:delete' => _x('button', 'Put in trashbin'),
        'MassiveAction:purge' => _x('button', 'Delete permanently'),
        'MassiveAction:restore' => _x('button', 'Restore'),
        'MassiveAction:amend_comment' => __('Amend comment'),
        'MassiveAction:add_note' => __('Add note'),
    ],
]);
