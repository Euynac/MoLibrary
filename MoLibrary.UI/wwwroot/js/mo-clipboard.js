(function() {
    window.MoClipboard = window.MoClipboard || {};

    window.MoClipboard.copyText = async function(text) {
        if (!text) return false;
        try {
            if (navigator.clipboard && navigator.clipboard.writeText) {
                await navigator.clipboard.writeText(text);
                return true;
            }
            // Fallback for HTTP (non-secure context)
            const textArea = document.createElement("textarea");
            textArea.value = text;
            textArea.style.cssText = "position:fixed;left:-999999px;top:-999999px";
            document.body.appendChild(textArea);
            textArea.focus();
            textArea.select();
            try {
                const success = document.execCommand('copy');
                document.body.removeChild(textArea);
                return success;
            } catch {
                document.body.removeChild(textArea);
                return false;
            }
        } catch {
            return false;
        }
    };
})();
