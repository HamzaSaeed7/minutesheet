window.quillInterop = (function () {
    const editors = {};

    let activeRecognition = null;
    let isRecording = false;
    let sessionStartIndex = 0;
    let sessionLength = 0;
    let activeLanguage = '';
    let activeQuill = null;
    let sessionTranscript = '';

    function toggleDictation(language, buttonElement, quillInstance) {
        if (isRecording) {
            isRecording = false;
            if (activeRecognition) {
                activeRecognition.stop();
            }
            buttonElement.classList.remove('listening');
            applyFormattingOnStop();
            activeRecognition = null;
            return;
        }

        if (!('webkitSpeechRecognition' in window) && !('SpeechRecognition' in window)) {
            alert('Your browser does not support speech recognition. Please try Google Chrome.');
            return;
        }
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        activeRecognition = new SpeechRecognition();

        activeRecognition.lang = language;
        activeRecognition.interimResults = false;
        activeRecognition.continuous = true;
        activeRecognition.maxAlternatives = 1;

        activeLanguage = language;
        activeQuill = quillInstance;
        sessionTranscript = '';
        sessionLength = 0;

        const range = quillInstance.getSelection(true);
        sessionStartIndex = range ? range.index : quillInstance.getLength();

        activeRecognition.onstart = function() {
            isRecording = true;
            buttonElement.classList.add('listening');
        };

        activeRecognition.onresult = function(event) {
            if (event.results.length > 0) {
                let latestResultIndex = event.results.length - 1;
                let transcript = event.results[latestResultIndex][0].transcript;
                let textToInsert = transcript + ' ';

                let insertPos = sessionStartIndex + sessionLength;
                quillInstance.insertText(insertPos, textToInsert);
                sessionLength += textToInsert.length;
                quillInstance.setSelection(insertPos + textToInsert.length);

                sessionTranscript += textToInsert;
            }
        };

        activeRecognition.onerror = function(event) {
            console.error('Speech recognition error', event.error);
        };

        activeRecognition.onend = function() {
            if (isRecording && activeRecognition) {
                // Browser stopped recognition automatically (e.g., silence).
                // Auto-restart to enforce true continuous speech until user stops it.
                try {
                    activeRecognition.start();
                } catch (e) {
                    console.error('Failed to restart recognition:', e);
                }
            }
        };

        activeRecognition.start();
    }

    function applyFormattingOnStop() {
        if (sessionLength > 0 && activeQuill) {
            let cleanTranscript = sessionTranscript.trimEnd();
            let formattedText = formatNouns(cleanTranscript, activeLanguage);
            formattedText += ' ';

            activeQuill.deleteText(sessionStartIndex, sessionLength);
            activeQuill.insertText(sessionStartIndex, formattedText);
            activeQuill.setSelection(sessionStartIndex + formattedText.length);
        }
        sessionLength = 0;
        sessionTranscript = '';
    }

    function formatNouns(text, language) {
        let lines = text.split('\n');

        for (let i = 0; i < lines.length; i++) {
            if (i === 0 || i === 2) {
                let targetText = lines[i].trim();
                if (targetText.length === 0) continue;

                if (language === 'en-US' && typeof window.nlp !== 'undefined') {
                    let extractedNouns = window.nlp(targetText).nouns().out('array');
                    if (extractedNouns.length > 0) {
                        lines[i] = lines[i] + '\n' + extractedNouns.join(', ');
                    }
                } else {
                    let words = targetText.split(/\s+/).filter(w => w.length > 3);
                    if (words.length > 0) {
                        lines[i] = lines[i] + '\n' + words.join(', ');
                    }
                }
            }
        }

        return lines.join('\n');
    }

    return {
        init: function (elementId, initialHtml) {
            const el = document.getElementById(elementId);
            if (!el) {
                return;
            }
            if (el.classList.contains('ql-container') && el.querySelector('.ql-editor')) {
                return;
            }
            delete editors[elementId];
            const quill = new Quill(el, {
                theme: 'snow',
                placeholder: 'Write the minute sheet description...',
                modules: {
                    toolbar: {
                        container: [
                            [{ header: [1, 2, 3, false] }],
                            ['bold', 'italic', 'underline', 'strike'],
                            [{ list: 'ordered' }, { list: 'bullet' }],
                            [{ indent: '-1' }, { indent: '+1' }],
                            ['link', 'blockquote'],
                            ['clean']
                        ]
                    }
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

        // Dictate into the editor via the browser's speech-recognition engine,
        // or stop an in-progress session. The optional `language` selects the
        // recognition locale (e.g. 'en-US' or 'ur-PK').
        toggleDictation: function (elementId, btn, language) {
            const quill = editors[elementId];
            if (!quill) {
                return;
            }
            toggleDictation(language || 'en-US', btn, quill);
        },

        destroy: function (elementId) {
            // Stop any active dictation session when the editor goes away.
            if (activeRecognition) {
                try { activeRecognition.stop(); } catch (_) { }
                activeRecognition = null;
                isRecording = false;
            }
            delete editors[elementId];
        },

        downloadFile: function (fileName, mimeType, base64) {
            const bytes = Uint8Array.from(atob(base64), (c) => c.charCodeAt(0));
            const blob = new Blob([bytes], { type: mimeType });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = fileName;
            document.body.appendChild(a);
            a.click();
            setTimeout(() => {
                document.body.removeChild(a);
                URL.revokeObjectURL(url);
            }, 100);
        }
    };
})();
