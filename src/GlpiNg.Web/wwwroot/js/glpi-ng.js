window.glpiNg = {
    hideModal: function (id) {
        var el = document.getElementById(id);
        if (!el) return;
        var modal = bootstrap.Modal.getInstance(el) || new bootstrap.Modal(el);
        modal.hide();
    },
    copyToClipboard: function (text) {
        return navigator.clipboard.writeText(text);
    }
};
