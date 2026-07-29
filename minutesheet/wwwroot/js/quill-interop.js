window.quillInterop = (function () {
    const editors = {};

    return {
        init: function (elementId, initialHtml) {
            const el = document.getElementById(elementId);
            if (!el) {
                return;
            }
            // If this exact element is already a live Quill editor, do nothing.
            // (Quill turns the target into .ql-container with a .ql-editor child.)
            if (el.classList.contains('ql-container') && el.querySelector('.ql-editor')) {
                return;
            }
            // Otherwise drop any stale instance. Blazor's enhanced navigation keeps
            // this module on `window` but replaces the DOM, so a cached editor here
            // points at a detached node — re-initialise on the fresh element.
            delete editors[elementId];
            const quill = new Quill(el, {
                theme: 'snow',
                placeholder: 'Write the minute sheet description...',
                modules: {
                    toolbar: [
                        [{ header: [1, 2, 3, false] }],
                        ['bold', 'italic', 'underline', 'strike'],
                        [{ list: 'ordered' }, { list: 'bullet' }],
                        [{ indent: '-1' }, { indent: '+1' }],
                        ['link', 'blockquote'],
                        ['clean']
                    ]
                }
            });
            if (initialHtml) {
                quill.clipboard.dangerouslyPasteHTML(initialHtml);
            }
            editors[elementId] = quill;
        },

        getHtml: function (elementId) {
            const quill = editors[elementId];
            if (!quill) {
                return '';
            }
            const html = quill.getSemanticHTML();
            // Treat an empty editor as empty string.
            const text = quill.getText().trim();
            return text.length === 0 ? '' : html;
        },

        destroy: function (elementId) {
            delete editors[elementId];
        }
    };
})();
