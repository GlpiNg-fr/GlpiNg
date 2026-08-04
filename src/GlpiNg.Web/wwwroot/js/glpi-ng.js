window.glpiNg = {
    hideModal: function (id) {
        var el = document.getElementById(id);
        if (!el) return;
        // Pas de window.bootstrap global exposé par le bundle Tabler chargé dans App.razor :
        // on simule le clic sur le bouton de fermeture plutôt que d'instancier bootstrap.Modal.
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
    }
};
