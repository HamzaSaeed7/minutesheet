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
                            ['clean'],
                            ['dictateEn', 'dictateUr']
                        ],
                        handlers: {
                            'dictateEn': function() {
                                const btn = this.container.querySelector('.ql-dictateEn');
                                toggleDictation('en-US', btn, quill);
                            },
                            'dictateUr': function() {
                                const btn = this.container.querySelector('.ql-dictateUr');
                                toggleDictation('ur-PK', btn, quill);
                            }
                        }
                    }
                }
            });
            if (initialHtml) {
                quill.clipboard.dangerouslyPasteHTML(initialHtml);
            }
            editors[elementId] = quill;

            const dictateEnBtn = quill.getModule('toolbar').container.querySelector('.ql-dictateEn');
            if (dictateEnBtn) { dictateEnBtn.innerText = '🎙️ EN'; dictateEnBtn.title = 'Dictate English'; }
            
            const dictateUrBtn = quill.getModule('toolbar').container.querySelector('.ql-dictateUr');
            if (dictateUrBtn) { dictateUrBtn.innerText = '🎤 UR'; dictateUrBtn.title = 'Dictate Urdu'; }
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
        // or stop an in-progress session. Recognized phrases are inserted at the
        // caret (or appended) as the user speaks.
        toggleDictation: function (elementId, btn) {
            const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
            if (!SR) {
                alert('Speech recognition is not supported in this browser. Try Chrome or Edge.');
                return;
            }
            // A second click stops the active session.
            if (this._recognizer) {
                this._recognizer.stop();
                return;
            }
            const quill = editors[elementId];
            if (!quill) {
                return;
            }

            const rec = new SR();
            rec.lang = 'en-US';
            rec.continuous = true;      // keep listening across pauses
            rec.interimResults = false; // only commit finalized phrases
            this._recognizer = rec;
            this._setDictateBtn(btn, true);

            rec.onresult = (e) => {
                let phrase = '';
                for (let i = e.resultIndex; i < e.results.length; i++) {
                    if (e.results[i].isFinal) {
                        phrase += e.results[i][0].transcript;
                    }
                }
                phrase = phrase.trim();
                if (!phrase) {
                    return;
                }
                // Insert at the caret, falling back to the end of the document.
                const sel = quill.getSelection();
                let index = sel ? sel.index : Math.max(0, quill.getLength() - 1);
                // Add a leading space if we're not at the start and there isn't
                // already whitespace before the caret.
                const prevChar = index > 0 ? quill.getText(index - 1, 1) : '';
                if (prevChar && !/\s/.test(prevChar)) {
                    phrase = ' ' + phrase;
                }
                quill.insertText(index, phrase, 'user');
                quill.setSelection(index + phrase.length, 0);
            };

            rec.onerror = (e) => {
                if (e.error === 'not-allowed' || e.error === 'service-not-allowed') {
                    alert('Microphone access was blocked. Allow it in your browser to use dictation.');
                }
            };

            rec.onend = () => {
                this._recognizer = null;
                this._setDictateBtn(btn, false);
            };

            rec.start();
        },

        _setDictateBtn: function (btn, active) {
            if (!btn) return;
            btn.classList.toggle('dictating', active);
            const label = btn.querySelector('.dictate-label');
            if (label) label.textContent = active ? 'Stop' : 'Dictate';
        },

        destroy: function (elementId) {
            // Stop any active dictation session when the editor goes away.
            if (this._recognizer) {
                try { this._recognizer.stop(); } catch (_) { }
                this._recognizer = null;
            }
            delete editors[elementId];
        }
    };
})();
