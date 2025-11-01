(function () {
    window.MoLogging = window.MoLogging || {};

    window.MoLogging.scrollToBottom = function (element) {
        if (!element) {
            return;
        }

        try {
            element.scrollTop = element.scrollHeight;
        } catch (error) {
            console.error("Failed to scroll log viewer", error);
        }
    };
})();
