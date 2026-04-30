export function initComposerKeyboard(root) {
    const textArea = root?.querySelector('textarea');
    if (!textArea) {
        return {
            setPickerOpen: () => {},
            dispose: () => {}
        };
    }

    let isPickerOpen = false;

    const handleKeyDown = event => {
        if (event.defaultPrevented) {
            return;
        }

        if (isPickerOpen && shouldPreventPickerKey(event)) {
            event.preventDefault();
            return;
        }

        if (!isPickerOpen && event.key === 'Enter' && !event.shiftKey) {
            event.preventDefault();
        }
    };

    textArea.addEventListener('keydown', handleKeyDown);

    return {
        setPickerOpen: value => {
            isPickerOpen = value === true;
        },
        dispose: () => {
            textArea.removeEventListener('keydown', handleKeyDown);
        }
    };
}

function shouldPreventPickerKey(event) {
    return event.key === 'ArrowDown'
        || event.key === 'ArrowUp'
        || event.key === 'Tab'
        || event.key === 'Escape'
        || (event.key === 'Enter' && !event.shiftKey);
}
