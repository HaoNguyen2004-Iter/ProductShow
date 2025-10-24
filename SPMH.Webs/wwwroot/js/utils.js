(function (window, $) {
    window.Spmh = window.Spmh || {};
    function toJq(root) {
        return root instanceof $ ? root : $(root);
    }
    function reader(root) {
        const $root = toJq(root || document);

        const read = (name) => ($root.find(`[name="${name}"]`).val() ?? '').toString().trim();

        const readNumber = (name, defaultValue = 0) => {
            const v = $root.find(`[name="${name}"]`).val();
            if (v === '' || v == null) return defaultValue;
            const n = Number(v);
            return Number.isFinite(n) ? n : defaultValue;
        };

        const readChecked = (name) => $root.find(`[name="${name}"]`).is(':checked');

        return { read, readNumber, readChecked };
    }
    function read(root, name) { return reader(root).read(name); }
    function readNumber(root, name, def) { return reader(root).readNumber(name, def); }
    function readChecked(root, name) { return reader(root).readChecked(name); }

    window.Spmh.fields = { reader, read, readNumber, readChecked };
})(window, jQuery);