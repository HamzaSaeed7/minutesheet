window.quillInterop = (function () {
    const editors = {};

    let activeMediaRecorder = null;
    let activeMediaStream = null;
    let isRecording = false;

    async function toggleMediaRecorderDictation(language, buttonElement, quillInstance) {
        const label = buttonElement.querySelector('.dictate-label');
        if (isRecording && activeMediaRecorder) {
            activeMediaRecorder.stop();
            return;
        }

        if (!navigator.mediaDevices?.getUserMedia || !window.MediaRecorder) {
            alert('Audio recording is not supported by this browser. Please use a current version of Chrome, Edge, Firefox, or Safari.');
            return;
        }

        try {
            activeMediaStream = await navigator.mediaDevices.getUserMedia({
                audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true }
            });
            const mimeType = MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
                ? 'audio/webm;codecs=opus'
                : undefined;
            const chunks = [];
            activeMediaRecorder = new MediaRecorder(activeMediaStream, mimeType ? { mimeType } : undefined);
            activeMediaRecorder.ondataavailable = event => {
                if (event.data.size > 0) chunks.push(event.data);
            };
            activeMediaRecorder.onstop = async () => {
                isRecording = false;
                buttonElement.classList.remove('listening');
                if (label) label.innerText = 'Transcribing...';
                activeMediaStream?.getTracks().forEach(track => track.stop());
                activeMediaStream = null;

                try {
                    const audio = new Blob(chunks, { type: activeMediaRecorder.mimeType || 'audio/webm' });
                    const form = new FormData();
                    const extension = audio.type.includes('mp4') ? 'mp4' : 'webm';
                    form.append('audio', audio, `recording.${extension}`);
                    form.append('language', language);
                    const response = await fetch('/api/transcriptions', { method: 'POST', body: form, credentials: 'same-origin' });
                    const result = await response.json().catch(() => ({}));
                    if (!response.ok) throw new Error(result.error || result.detail || 'Transcription failed.');
                    if (result.text) {
                        const range = quillInstance.getSelection(true);
                        const insertPos = range ? range.index : quillInstance.getLength();
                        const text = `${result.text} `;
                        quillInstance.insertText(insertPos, text);
                        quillInstance.setSelection(insertPos + text.length);
                    }
                } catch (error) {
                    console.error('Transcription error', error);
                    alert(error.message || 'Unable to transcribe this recording.');
                } finally {
                    activeMediaRecorder = null;
                    if (label) label.innerText = 'Dictate';
                }
            };
            activeMediaRecorder.start();
            isRecording = true;
            buttonElement.classList.add('listening');
            if (label) label.innerText = 'Stop recording';
        } catch (error) {
            console.error('Microphone error', error);
            alert('Microphone access is required to dictate. Please allow it in your browser settings.');
            activeMediaStream?.getTracks().forEach(track => track.stop());
            activeMediaStream = null;
        }
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

        // Record audio in the browser and transcribe it server-side with Whisper.
        toggleMediaRecorderDictation: function (elementId, btn, language) {
            const quill = editors[elementId];
            if (!quill) {
                return;
            }
            toggleMediaRecorderDictation(language || 'en-US', btn, quill);
        },

        destroy: function (elementId) {
            // Stop any active dictation session when the editor goes away.
            if (activeMediaRecorder && isRecording) activeMediaRecorder.stop();
            delete editors[elementId];
        },

        getElementText: function (element) {
            return element ? (element.innerText || element.textContent || "").trim() : "";
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
