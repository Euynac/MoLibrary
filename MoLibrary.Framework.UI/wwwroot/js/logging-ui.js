(function () {
    window.MoLogging = window.MoLogging || {};

    window.MoLogging.scrollToBottom = function (element) {
        if (!element) {
            return;
        }

        try {
            // Use requestAnimationFrame to ensure DOM is updated before scrolling
            requestAnimationFrame(() => {
                requestAnimationFrame(() => {
                    element.scrollTop = element.scrollHeight;
                });
            });
        } catch (error) {
            console.error("Failed to scroll log viewer", error);
        }
    };

    /**
     * 复制文本到剪贴板
     * @param {string} text - 要复制的文本
     * @returns {Promise<boolean>} 复制是否成功
     */
    window.MoLogging.copyToClipboard = async function (text) {
        if (!text) {
            return false;
        }

        try {
            if (navigator.clipboard && navigator.clipboard.writeText) {
                await navigator.clipboard.writeText(text);
                return true;
            } else {
                // 降级方案：使用旧的 execCommand 方法
                const textArea = document.createElement("textarea");
                textArea.value = text;
                textArea.style.position = "fixed";
                textArea.style.left = "-999999px";
                textArea.style.top = "-999999px";
                document.body.appendChild(textArea);
                textArea.focus();
                textArea.select();

                try {
                    const successful = document.execCommand('copy');
                    document.body.removeChild(textArea);
                    return successful;
                } catch (err) {
                    document.body.removeChild(textArea);
                    console.error("Failed to copy text using fallback method", err);
                    return false;
                }
            }
        } catch (error) {
            console.error("Failed to copy text to clipboard", error);
            return false;
        }
    };

    /**
     * 获取当前选中的文本
     * @returns {string} 选中的文本，如果没有选中则返回空字符串
     */
    window.MoLogging.getSelectedText = function () {
        try {
            const selection = window.getSelection();
            if (selection) {
                return selection.toString().trim();
            }
            return "";
        } catch (error) {
            console.error("Failed to get selected text", error);
            return "";
        }
    };

    /**
     * 滚动元素到顶部
     * @param {HTMLElement} element - 要滚动的元素
     */
    window.MoLogging.scrollToTop = function (element) {
        if (!element) {
            return;
        }

        try {
            element.scrollTop = 0;
        } catch (error) {
            console.error("Failed to scroll to top", error);
        }
    };

    /**
     * 滚动到指定的日志行索引
     * @param {HTMLElement} container - 滚动容器元素
     * @param {number} lineIndex - 日志行索引
     */
    window.MoLogging.scrollToLineIndex = function (container, lineIndex) {
        if (!container) {
            return;
        }

        try {
            // Find the log entry element by data-line-index attribute
            const targetElement = container.querySelector(`[data-line-index="${lineIndex}"]`);

            if (targetElement) {
                // Calculate the position to scroll to (center the element in view)
                const containerRect = container.getBoundingClientRect();
                const elementRect = targetElement.getBoundingClientRect();
                const containerScrollTop = container.scrollTop;

                // Calculate offset to center the element
                const elementTop = elementRect.top - containerRect.top + containerScrollTop;
                const centerOffset = (containerRect.height - elementRect.height) / 2;
                const scrollTo = elementTop - centerOffset;

                // Smooth scroll to the target position
                container.scrollTo({
                    top: scrollTo,
                    behavior: 'smooth'
                });
            }
        } catch (error) {
            console.error("Failed to scroll to line index", error);
        }
    };

    /**
     * 添加滚动事件监听，检测滚动到顶部
     * @param {HTMLElement} container - 滚动容器元素
     * @param {object} dotNetRef - DotNetObjectReference
     * @param {string} methodName - 要调用的.NET方法名
     * @param {number} threshold - 触发阈值（像素），默认为50
     */
    window.MoLogging.addScrollTopListener = function (container, dotNetRef, methodName, threshold = 50) {
        if (!container || !dotNetRef) {
            return;
        }

        let isLoading = false;
        let lastScrollTop = container.scrollTop;

        const handleScroll = function () {
            const scrollTop = container.scrollTop;
            const isScrollingUp = scrollTop < lastScrollTop;
            lastScrollTop = scrollTop;

            // 检测是否滚动到顶部附近且正在向上滚动
            if (isScrollingUp && scrollTop <= threshold && !isLoading) {
                isLoading = true;
                dotNetRef.invokeMethodAsync(methodName)
                    .then(() => {
                        isLoading = false;
                    })
                    .catch((error) => {
                        console.error("Error invoking load more:", error);
                        isLoading = false;
                    });
            }
        };

        container.addEventListener('scroll', handleScroll, { passive: true });
    };

    /**
     * 保存并恢复滚动位置
     * @param {HTMLElement} container - 滚动容器元素
     * @param {number} previousLineCount - 加载前的行数
     * @param {number} newLineCount - 加载后的新增行数
     */
    window.MoLogging.preserveScrollPosition = function (container, previousLineCount, newLineCount) {
        if (!container || newLineCount === 0) {
            return;
        }

        try {
            // 找到第一个旧的日志条目（新加载的行数之后）
            const firstOldLineElement = container.querySelector(`[data-line-index="${newLineCount}"]`);

            if (firstOldLineElement) {
                // 滚动到原来的第一行位置
                firstOldLineElement.scrollIntoView({ block: 'start' });
            }
        } catch (error) {
            console.error("Failed to preserve scroll position", error);
        }
    };
})();
