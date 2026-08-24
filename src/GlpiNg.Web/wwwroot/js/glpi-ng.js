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
    }
};
