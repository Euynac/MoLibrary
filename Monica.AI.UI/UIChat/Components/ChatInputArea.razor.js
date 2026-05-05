const SKILL_PREFIX = "[$skills:";
const MCP_PREFIX = "[$mcp:";
const TOKEN_END = "]";

export function initComposerKeyboard(root, dotNetRef, initialValue, placeholder) {
    const editor = root?.querySelector("[data-composer-editor]");
    if (!editor) {
        return {
            setPickerOpen: () => {},
            setDisabled: () => {},
            setValue: () => {},
            dispose: () => {}
        };
    }

    let isPickerOpen = false;
    let currentValue = initialValue ?? "";

    editor.setAttribute("data-placeholder", placeholder ?? "");
    renderEditor(editor, currentValue, currentValue.length);

    const notifyInput = () => {
        currentValue = readEditorText(editor);
        const caretIndex = getCaretIndex(editor);
        dotNetRef.invokeMethodAsync("HandleComposerInput", currentValue, caretIndex);
    };

    const handleInput = () => {
        notifyInput();
    };

    const handleKeyDown = event => {
        if (event.defaultPrevented) {
            return;
        }

        if (event.key === "Enter" && event.shiftKey) {
            event.preventDefault();
            insertPlainText("\n");
            notifyInput();
            return;
        }

        if (isPickerOpen && shouldPreventPickerKey(event)) {
            event.preventDefault();
            return;
        }

        if (!isPickerOpen && event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
        }
    };

    const handlePaste = event => {
        const text = event.clipboardData?.getData("text/plain");
        if (text === undefined) {
            return;
        }

        event.preventDefault();
        insertPlainText(text);
    };

    editor.addEventListener("input", handleInput);
    editor.addEventListener("keydown", handleKeyDown);
    editor.addEventListener("paste", handlePaste);

    return {
        setPickerOpen: value => {
            isPickerOpen = value === true;
        },
        setDisabled: value => {
            editor.setAttribute("contenteditable", value === true ? "false" : "true");
        },
        setValue: (value, caretIndex, focus) => {
            const nextValue = value ?? "";
            if (nextValue !== currentValue || (nextValue.length > 0 && editor.childNodes.length === 0)) {
                currentValue = nextValue;
                renderEditor(editor, currentValue, normalizeCaret(caretIndex, currentValue));
            } else if (focus === true) {
                setCaretIndex(editor, normalizeCaret(caretIndex, currentValue));
            }

            if (focus === true) {
                editor.focus();
            }
        },
        dispose: () => {
            editor.removeEventListener("input", handleInput);
            editor.removeEventListener("keydown", handleKeyDown);
            editor.removeEventListener("paste", handlePaste);
        }
    };
}

function shouldPreventPickerKey(event) {
    return event.key === "ArrowDown"
        || event.key === "ArrowUp"
        || event.key === "Tab"
        || event.key === "Escape"
        || (event.key === "Enter" && !event.shiftKey);
}

function renderEditor(editor, value, caretIndex) {
    editor.replaceChildren(...createEditorNodes(value));
    setCaretIndex(editor, normalizeCaret(caretIndex, value));
}

function createEditorNodes(value) {
    const nodes = [];
    for (const segment of parseReferenceSegments(value)) {
        if (segment.kind === null) {
            nodes.push(document.createTextNode(segment.text));
            continue;
        }

        const badge = document.createElement("span");
        badge.className = `composer-reference-token ${segment.kind}`;
        badge.contentEditable = "false";
        badge.dataset.token = segment.token;
        badge.textContent = `${segment.kind}:${segment.text}`;
        nodes.push(badge);
    }

    return nodes;
}

function parseReferenceSegments(value) {
    if (!value) {
        return [];
    }

    const segments = [];
    let index = 0;

    while (index < value.length) {
        const tokenStart = value.indexOf("[$", index);
        if (tokenStart < 0) {
            addTextSegment(segments, value.slice(index));
            break;
        }

        if (tokenStart > index) {
            addTextSegment(segments, value.slice(index, tokenStart));
        }

        const tokenEnd = value.indexOf(TOKEN_END, tokenStart + 2);
        if (tokenEnd < 0) {
            addTextSegment(segments, value.slice(tokenStart));
            break;
        }

        const token = value.slice(tokenStart, tokenEnd + 1);
        const parsed = parseReferenceToken(token);
        if (parsed === null) {
            addTextSegment(segments, token);
        } else {
            segments.push(parsed);
        }

        index = tokenEnd + 1;
    }

    return segments;
}

function parseReferenceToken(token) {
    if (token.startsWith(SKILL_PREFIX)) {
        return createReferenceSegment("skills", SKILL_PREFIX, token);
    }

    if (token.startsWith(MCP_PREFIX)) {
        return createReferenceSegment("mcp", MCP_PREFIX, token);
    }

    return null;
}

function createReferenceSegment(kind, prefix, token) {
    const name = token.slice(prefix.length, -1).trim();
    if (name.length === 0) {
        return null;
    }

    return {
        kind,
        text: name,
        token
    };
}

function addTextSegment(segments, text) {
    if (text.length > 0) {
        segments.push({
            kind: null,
            text,
            token: null
        });
    }
}

function readEditorText(editor) {
    let value = "";

    for (const node of editor.childNodes) {
        value += readNodeText(node);
    }

    return value;
}

function readNodeText(node) {
    if (node.nodeType === Node.TEXT_NODE) {
        return node.nodeValue ?? "";
    }

    if (node.nodeType !== Node.ELEMENT_NODE) {
        return "";
    }

    if (node.dataset?.token) {
        return node.dataset.token;
    }

    if (node.tagName === "BR") {
        return "\n";
    }

    let value = "";
    for (const child of node.childNodes) {
        value += readNodeText(child);
    }

    return value;
}

function getCaretIndex(editor) {
    const selection = window.getSelection();
    if (!selection || selection.rangeCount === 0) {
        return readEditorText(editor).length;
    }

    return getNodePosition(editor, selection.anchorNode, selection.anchorOffset);
}

function getNodePosition(editor, targetNode, targetOffset) {
    if (targetNode === editor) {
        return measureChildNodes(editor, targetOffset);
    }

    let total = 0;
    const walker = document.createTreeWalker(
        editor,
        NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT
    );

    let node = walker.nextNode();
    while (node) {
        if (node === targetNode) {
            if (node.nodeType === Node.TEXT_NODE) {
                return total + targetOffset;
            }

            return total;
        }

        if (node.nodeType === Node.ELEMENT_NODE && node.dataset?.token) {
            if (node.contains(targetNode)) {
                return total;
            }

            total += node.dataset.token.length;
            node = walker.nextSibling();
            continue;
        }

        if (node.nodeType === Node.TEXT_NODE) {
            total += node.nodeValue?.length ?? 0;
        }

        node = walker.nextNode();
    }

    return total;
}

function measureChildNodes(editor, offset) {
    let total = 0;
    const max = Math.min(offset, editor.childNodes.length);
    for (let i = 0; i < max; i++) {
        total += readNodeText(editor.childNodes[i]).length;
    }

    return total;
}

function setCaretIndex(editor, caretIndex) {
    const range = document.createRange();
    const position = findCaretPosition(editor, caretIndex);
    range.setStart(position.node, position.offset);
    range.collapse(true);

    const selection = window.getSelection();
    selection?.removeAllRanges();
    selection?.addRange(range);
}

function findCaretPosition(editor, caretIndex) {
    let remaining = caretIndex;

    for (const node of editor.childNodes) {
        const result = findCaretPositionInNode(node, remaining);
        if (result.found) {
            return result.position;
        }

        remaining -= result.length;
    }

    return {
        node: editor,
        offset: editor.childNodes.length
    };
}

function findCaretPositionInNode(node, remaining) {
    if (node.nodeType === Node.TEXT_NODE) {
        const length = node.nodeValue?.length ?? 0;
        if (remaining <= length) {
            return {
                found: true,
                position: {
                    node,
                    offset: remaining
                },
                length
            };
        }

        return { found: false, position: null, length };
    }

    if (node.nodeType !== Node.ELEMENT_NODE) {
        return { found: false, position: null, length: 0 };
    }

    if (node.dataset?.token) {
        const tokenLength = node.dataset.token.length;
        if (remaining <= tokenLength) {
            return {
                found: true,
                position: positionNearToken(node, remaining, tokenLength),
                length: tokenLength
            };
        }

        return { found: false, position: null, length: tokenLength };
    }

    let total = 0;
    for (const child of node.childNodes) {
        const result = findCaretPositionInNode(child, remaining - total);
        if (result.found) {
            return result;
        }

        total += result.length;
    }

    return { found: false, position: null, length: total };
}

function positionNearToken(tokenNode, remaining, tokenLength) {
    const parent = tokenNode.parentNode;
    const index = Array.prototype.indexOf.call(parent.childNodes, tokenNode);
    return {
        node: parent,
        offset: remaining < tokenLength ? index : index + 1
    };
}

function normalizeCaret(caretIndex, value) {
    if (!Number.isFinite(caretIndex)) {
        return value.length;
    }

    return Math.max(0, Math.min(caretIndex, value.length));
}

function insertPlainText(text) {
    document.execCommand("insertText", false, text);
}
