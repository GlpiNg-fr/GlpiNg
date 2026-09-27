<?php

/**
 * Amorce minimale de GLPI sans base de données : juste assez pour exécuter les définitions
 * statiques dont les API ont besoin (schémas de l'API v2, options de recherche, tables).
 *
 * GLPI lit ces définitions dans son propre code PHP ; plutôt que de les recopier à la main, on
 * fait tourner ce code sur une archive officielle, avec une base factice qui répond d'après le
 * schéma d'installation (install/mysql/glpi-empty.sql).
 */

$glpi_root = $argv[1] ?? getenv('GLPI_SOURCE') ?: '';
if ($glpi_root === '' || !is_file($glpi_root . '/vendor/autoload.php')) {
    fwrite(STDERR, "Usage : php generate.php <dossier d'une archive GLPI décompressée> <dossier de sortie>\n");
    exit(1);
}
define('GLPING_GLPI_ROOT', realpath($glpi_root));

require GLPING_GLPI_ROOT . '/vendor/autoload.php';

try {
    new Glpi\Application\SystemConfigurator(GLPING_GLPI_ROOT, 'production');
} catch (Throwable $e) {
    fwrite(STDERR, "SystemConfigurator: " . $e->getMessage() . "\n");
}

global $CFG_GLPI, $DB, $PLUGIN_HOOKS, $GLPI_CACHE;
$PLUGIN_HOOKS = [];

/** Colonnes de chaque table, lues dans les CREATE TABLE du schéma d'installation. */
function glping_parse_schema(string $sql): array
{
    $tables = [];
    preg_match_all('/CREATE TABLE `([a-z0-9_]+)` \((.*?)\n\) ENGINE/s', $sql, $blocks, PREG_SET_ORDER);
    foreach ($blocks as [$all, $table, $body]) {
        $cols = [];
        foreach (explode("\n", $body) as $line) {
            if (!preg_match('/^\s*`([a-z0-9_]+)` ([a-z]+(?:\([^)]*\))?(?: unsigned)?)(.*?),?\s*$/i', $line, $c)) {
                continue;
            }
            $rest = $c[3];
            $default = null;
            $has_default = false;
            if (preg_match("/DEFAULT ('(?:[^']|'')*'|[^ ,]+)/", $rest, $d)) {
                $has_default = true;
                $default = $d[1] === 'NULL' ? null : trim($d[1], "'");
            }
            $cols[$c[1]] = [
                'Field' => $c[1],
                'Type' => strtolower($c[2]),
                'Null' => str_contains($rest, 'NOT NULL') ? 'NO' : 'YES',
                'Default' => $default,
                'has_default' => $has_default,
                'auto_increment' => str_contains($rest, 'AUTO_INCREMENT'),
            ];
        }
        $tables[$table] = $cols;
    }
    return $tables;
}
$GLOBALS['GLPING_SCHEMA'] = glping_parse_schema(file_get_contents(GLPING_GLPI_ROOT . '/install/mysql/glpi-empty.sql'));

final class GlpingFakeCache
{
    public array $data = [];
    public function get($k, $d = null) { return $this->data[$k] ?? $d; }
    public function set($k, $v, $ttl = null) { $this->data[$k] = $v; return true; }
    public function has($k) { return isset($this->data[$k]); }
    public function delete($k) { unset($this->data[$k]); return true; }
    public function getMultiple($keys, $d = null) { $r = []; foreach ($keys as $k) { $r[$k] = $this->get($k, $d); } return $r; }
    public function setMultiple($values, $ttl = null) { foreach ($values as $k => $v) { $this->data[$k] = $v; } return true; }
    public function deleteMultiple($keys) { foreach ($keys as $k) { unset($this->data[$k]); } return true; }
    public function clear() { $this->data = []; return true; }
}
$GLPI_CACHE = new GlpingFakeCache();

/** Base factice : aucune ligne, mais la structure des tables est connue. */
final class GlpingFakeDB
{
    public function request(...$a) { return new ArrayIterator([]); }
    public function listFields($table, $usecache = true) { return $GLOBALS['GLPING_SCHEMA'][$table] ?? []; }
    public function fieldExists($table, $field, $usecache = true) { return isset($GLOBALS['GLPING_SCHEMA'][$table][$field]); }
    public function tableExists($table, $usecache = true) { return isset($GLOBALS['GLPING_SCHEMA'][$table]); }
    public static function quoteName($n) { return "`$n`"; }
    public function quote($v) { return "'" . addslashes((string) $v) . "'"; }
    public static function quoteValue($v) { return is_numeric($v) ? $v : "'" . addslashes((string) $v) . "'"; }
    public function __call($n, $a) { return null; }
    public static function __callStatic($n, $a) { return null; }
}
$DB = new GlpingFakeDB();

$_SESSION = [
    'glpi_use_mode' => 0,
    'glpiactiveprofile' => ['interface' => 'central', 'id' => 4, 'helpdesk_item_type' => []],
    'glpiID' => 2,
    'glpiname' => 'glpi',
    'glpilanguage' => 'en_GB',
    'glpiactive_entity' => 0,
    'glpiactiveentities' => [0],
    'glpiactiveentities_string' => "'0'",
    'glpi_currenttime' => date('Y-m-d H:i:s'),
];

// Données d'une installation neuve (profils, droits, préférences d'affichage, configuration).
$GLOBALS['GLPING_EMPTY_DATA'] = require GLPING_GLPI_ROOT . '/install/empty_data.php';
foreach ($GLOBALS['GLPING_EMPTY_DATA']['glpi_configs'] as $row) {
    if ($row['context'] !== 'core' || array_key_exists($row['name'], $CFG_GLPI)) {
        continue;
    }
    $v = $row['value'];
    if (is_string($v) && str_starts_with($v, '[')) {
        $decoded = json_decode($v, true);
        $v = is_array($decoded) ? $decoded : $v;
    }
    $CFG_GLPI[$row['name']] = $v;
}
$CFG_GLPI['url_base'] = 'http://localhost';
$CFG_GLPI['url_base_api'] = 'http://localhost/api.php';
