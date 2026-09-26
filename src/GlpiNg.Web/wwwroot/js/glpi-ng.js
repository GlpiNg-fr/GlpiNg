window.glpiNg = {
    // Utilisé quand la modale doit être pré-remplie côté C# (état du composant mis à jour) avant
    // ouverture — un simple data-bs-toggle="modal" sur le bouton déclencheur ne permet pas ça, car
    // le contenu affiché serait celui du rendu précédent. bootstrap.Modal.getOrCreateInstance
    // réutilise l'instance existante si la modale a déjà été ouverte via data-bs-toggle ailleurs.
    showModal: function (id) {
        var el = document.getElementById(id);
        if (!el) return;
        bootstrap.Modal.getOrCreateInstance(el).show();
    },
    hideModal: function (id) {
        var el = document.getElementById(id);
        if (!el) return;
        // On simule le clic sur le bouton de fermeture plutôt que d'instancier bootstrap.Modal
        // pour rester cohérent avec le HTML/data-bs-* existant.
        var closeButton = el.querySelector('[data-bs-dismiss="modal"]');
        if (closeButton) closeButton.click();
    },
    // Fait suivre à un journal qui se remplit (job de déploiement en cours) ses dernières lignes :
    // chaque rendu Blazor qui ajoute du contenu ramène en bas, sauf si l'utilisateur est remonté
    // lire plus haut — il reprend alors la main, jusqu'à ce qu'il redescende en bas. Idempotente :
    // appelée à chaque rendu, elle n'installe l'observateur qu'une fois par élément.
    // « Était en bas » se juge contre la hauteur d'avant l'ajout, relue à chaque mutation, plutôt
    // que sur l'événement scroll : le navigateur ne l'émet pas dans un onglet en arrière-plan, et
    // la position de lecture de l'utilisateur était alors écrasée au rafraîchissement suivant.
    followLog: function (el) {
        if (!(el instanceof Element) || el._glpiFollowLog) return;
        el._glpiFollowLog = true;
        el.scrollTop = el.scrollHeight;
        var previousHeight = el.scrollHeight;
        new MutationObserver(function () {
            var wasAtBottom = el.scrollTop + el.clientHeight >= previousHeight - 24;
            if (wasAtBottom) el.scrollTop = el.scrollHeight;
            previousHeight = el.scrollHeight;
        }).observe(el, { childList: true, subtree: true, characterData: true });
    },
    // Transmet au composant la largeur réelle d'un élément, puis chacune de ses variations : un
    // graphique SVG dessiné à sa vraie largeur garde un texte à sa vraie taille, là qu'un viewBox
    // fixe étiré à la largeur de la carte grossissait les étiquettes d'axe avec l'écran.
    // Idempotente par élément ; l'observateur disparaît avec l'élément.
    observeWidth: function (el, dotNetRef, method) {
        if (!(el instanceof Element) || el._glpiWidthObserver) return;
        var last = 0;
        el._glpiWidthObserver = new ResizeObserver(function (entries) {
            var width = Math.round(entries[0].contentRect.width);
            if (width > 0 && Math.abs(width - last) >= 4) {
                last = width;
                dotNetRef.invokeMethodAsync(method, width).catch(function () { });
            }
        });
        el._glpiWidthObserver.observe(el);
    },
    copyToClipboard: function (text) {
        return navigator.clipboard.writeText(text);
    },
    downloadFileFromStream: async function (fileName, contentType, streamRef) {
        const arrayBuffer = await streamRef.arrayBuffer();
        const blob = new Blob([arrayBuffer], { type: contentType || "application/octet-stream" });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = fileName ?? "";
        anchor.click();
        anchor.remove();
        URL.revokeObjectURL(url);
    },
    // Envoie chaque fichier sélectionné dans #inputId vers uploadUrl via un POST multipart
    // classique (XMLHttpRequest, pas fetch : seul XHR expose upload.onprogress), un fichier par
    // requête, l'un après l'autre. dotNetRef.OnUploadProgress(fileName, index, total, percent)
    // est appelé à chaque tick de progression. Volontairement hors du circuit SignalR de Blazor
    // (InputFile) : la limite de taille de message SignalR rend les gros fichiers peu fiables
    // quel que soit le réglage de buffer côté serveur (paquets de déploiement, plusieurs Go).
    // Résout avec le nombre de fichiers effectivement envoyés.
    uploadPackageFiles: function (inputId, uploadUrl, dotNetRef) {
        var input = document.getElementById(inputId);
        var files = input ? Array.prototype.slice.call(input.files) : [];
        var total = files.length;

        function uploadOne(index) {
            if (index >= total) {
                return Promise.resolve();
            }

            var file = files[index];
            return new Promise(function (resolve, reject) {
                var xhr = new XMLHttpRequest();
                xhr.open("POST", uploadUrl, true);
                xhr.upload.onprogress = function (e) {
                    if (e.lengthComputable) {
                        var percent = (e.loaded / e.total) * 100;
                        dotNetRef.invokeMethodAsync("OnUploadProgress", file.name, index, total, percent);
                    }
                };
                xhr.onload = function () {
                    if (xhr.status >= 200 && xhr.status < 300) resolve();
                    else {
                        var detail = (xhr.responseText || "").trim();
                        reject(new Error("HTTP " + xhr.status + (detail ? " : " + detail : "") + " (" + file.name + ")"));
                    }
                };
                xhr.onerror = function () { reject(new Error("Erreur réseau pendant l'envoi de " + file.name)); };
                var formData = new FormData();
                formData.append("file", file, file.name);
                xhr.send(formData);
            }).then(function () { return uploadOne(index + 1); });
        }

        return uploadOne(0).then(function () {
            if (input) input.value = "";
            return total;
        });
    },

    // --- Éditeur Markdown de la base de connaissances ---------------------------------------
    // Applique une commande de barre d'outils au <textarea> d'id donné et renvoie le texte
    // résultant avec la sélection à rétablir. Le C# reste maître de la valeur : il la réaffecte
    // au modèle puis redemande la sélection (voir setEditorSelection). Sans ce passage par JS,
    // on n'a accès ni au curseur ni à la portion sélectionnée, et la barre d'outils ne saurait
    // que concaténer en fin de texte.
    //
    // options : { before, after, placeholder } pour encadrer la sélection,
    //           { linePrefix } pour préfixer chaque ligne concernée (titres, listes, citations).
    editorCommand: function (id, options) {
        var el = document.getElementById(id);
        if (!el) return null;

        var value = el.value;
        var start = el.selectionStart;
        var end = el.selectionEnd;

        if (options.linePrefix) {
            var prefix = options.linePrefix;
            var ordered = prefix === "1. ";

            // On étend la sélection aux lignes entières : préfixer une demi-ligne n'a pas de sens.
            var lineStart = value.lastIndexOf("\n", start - 1) + 1;
            var lineEnd = value.indexOf("\n", end);
            if (lineEnd < 0) lineEnd = value.length;

            var lines = value.substring(lineStart, lineEnd).split("\n");
            var isPrefixed = function (line) {
                return ordered ? /^\d+\.\s/.test(line) : line.indexOf(prefix) === 0;
            };

            // Commande à bascule : si tout est déjà préfixé, on retire — c'est ce qu'attend
            // quiconque reclique sur « liste » en voyant sa liste déjà faite.
            var allPrefixed = lines.every(isPrefixed);
            var mapped = lines.map(function (line, index) {
                if (allPrefixed) {
                    return ordered ? line.replace(/^\d+\.\s/, "") : line.substring(prefix.length);
                }
                return (ordered ? (index + 1) + ". " : prefix) + line;
            });

            var replaced = mapped.join("\n");
            return {
                value: value.substring(0, lineStart) + replaced + value.substring(lineEnd),
                start: lineStart,
                end: lineStart + replaced.length
            };
        }

        var before = options.before || "";
        var after = options.after || "";
        // Sans sélection, on insère un libellé d'exemple et on le sélectionne : la frappe
        // suivante le remplace, ce qui évite de repositionner le curseur à la main.
        var selected = value.substring(start, end) || options.placeholder || "";
        var inserted = before + selected + after;

        return {
            value: value.substring(0, start) + inserted + value.substring(end),
            start: start + before.length,
            end: start + before.length + selected.length
        };
    },

    // Rend le focus et la sélection au <textarea> après que Blazor a réécrit sa valeur.
    setEditorSelection: function (id, start, end) {
        var el = document.getElementById(id);
        if (!el) return;
        el.focus();
        el.setSelectionRange(start, end);
    }
};
