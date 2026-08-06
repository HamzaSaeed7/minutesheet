# Minute Sheet App - Feature Report

This report provides a comprehensive breakdown of the two major features recently integrated into the Minute Sheet application: **AI Document Summarization** and **Audio Dictation / Transcription**.

---

## 1. AI Document Summarization

### 1.1. Technical Implementation Breakdown
When a user wants to generate a summary for a Minute Sheet, the process flows as follows:
1. **User Input & Trigger:** The user uploads an attachment (PDF or Word document), fills in the sheet's description via the Quill editor, and clicks the "Generate Summary" button in the `CreateSheet.razor` UI.
2. **Data Aggregation:** The UI passes the sheet's metadata (Category, Creator Name, Designation, Department, Employee No.), the rich text description (HTML), and the uploaded `IBrowserFile` to the `DocumentSummarizationService`.
3. **Text Extraction:**
   - **HTML Description:** The service uses Regular Expressions to strip HTML tags, converting the description into plain text.
   - **File Attachments:** The service reads the file stream into memory. For PDFs, it iterates through pages to extract text using the `PdfPig` library. For Word documents (`.doc`/`.docx`), it utilizes `DocumentFormat.OpenXml` to extract the inner text of the document body. 
   - **Sanitization:** The extracted text is sanitized to remove control characters or null bytes that could trigger AI hallucinations or safety filters.
4. **AI Processing:** The consolidated text is formatted into a prompt and sent to the OpenRouter API (`openrouter/free` model) via an HTTP POST request. A system prompt explicitly instructs the AI to generate a concise, professional summary in **English**, regardless of whether the source text is in English, Urdu, or Roman Urdu.
5. **Error & Rate Limit Handling:** The service specifically catches HTTP `429 Too Many Requests` errors to inform the user of rate limits. It also parses OpenRouter's `finish_reason` to detect and handle content flagged by safety filters (`content_filter`).
6. **Output:** The returned summary text is surfaced directly into a read-only textarea in the UI for the user's review.

### 1.2. Files Modified / Created
- **`minutesheet/Services/DocumentSummarizationService.cs`** [NEW]: The core service responsible for orchestrating text extraction from files/HTML, sanitizing the input, and communicating with the OpenRouter AI API.
- **`minutesheet/Components/Pages/Dashboard/CreateSheet.razor`** [MODIFIED]: Updated to include the "Generate Summary" button, the summary display textarea, and the event handler (`GenerateSummaryAsync`) that links the UI to the summarization service.
- **`minutesheet/minutesheet.csproj`** [MODIFIED]: Added NuGet package references for `UglyToad.PdfPig` and `DocumentFormat.OpenXml` to support document parsing.
- **`minutesheet/Program.cs`** [MODIFIED]: Registered the `DocumentSummarizationService` and configured its `HttpClient` into the application's dependency injection container.

### 1.3. Technologies & Libraries Used
- **Framework:** .NET 8.0 (Blazor Server)
- **PDF Extraction:** `UglyToad.PdfPig` NuGet package
- **Word Document Extraction:** `DocumentFormat.OpenXml` NuGet package
- **AI Service:** OpenRouter API (utilizing the `openrouter/free` model endpoint)

### 1.4. The "Supervisor Explanation" (Executive Summary)
> **AI Document Summarization Feature**
> "Our AI Document Summarization feature automates the tedious process of reading and summarizing long attachments and sheet descriptions. By integrating with OpenRouter's AI API, the Minute Sheet app can instantly read uploaded PDFs or Word documents—alongside the user's input—and provide a concise English summary, even if the original text contains Urdu or Roman Urdu. This saves significant time for managers and approvers, allowing them to quickly grasp the core request without wading through pages of raw text, all while handling rate limits and safety checks gracefully."

---

## 2. Audio Dictation / Transcription

### 2.1. Technical Implementation Breakdown
The dictation feature leverages the browser's native capabilities to perform real-time speech-to-text without requiring backend processing:
1. **Triggering Dictation:** The Quill rich text editor toolbar is customized with two new buttons: "🎙️ EN" (English) and "🎤 UR" (Urdu).
2. **Speech Recognition:** When a button is clicked, `quill-interop.js` instantiates the browser's native Web Speech API (`SpeechRecognition` or `webkitSpeechRecognition`), configuring the language (`en-US` or `ur-PK`) and setting it to continuous mode.
3. **Real-time Transcription:** As the user speaks, the API returns transcribed text chunks (`onresult`). These chunks are dynamically inserted into the Quill editor at the current cursor position, updating seamlessly as the user continues talking.
4. **Resiliency:** To enforce true continuous dictation, the script intercepts the `onend` event. If the browser stops listening prematurely (e.g., due to a brief pause in speech), the application automatically restarts the recognition engine until the user explicitly stops it.
5. **Post-Processing (Formatting):** When the dictation session is manually stopped, a `formatNouns` function is applied to the newly dictated text block. For English, it utilizes the Compromise NLP library (`window.nlp`) to extract key nouns. For Urdu, it applies a basic word-length filter. These extracted keywords are appended to the text, aiding in highlighting the most critical terms from the dictation.

### 2.2. Files Modified / Created
- **`minutesheet/wwwroot/js/quill-interop.js`** [MODIFIED]: Embedded the dictation state machine, initialized the `SpeechRecognition` object, handled Quill cursor positioning/text insertion during active speech, managed the auto-restart logic, and added the post-processing keyword extraction.
- **`minutesheet/Components/Pages/Dashboard/CreateSheet.razor`** [MODIFIED]: Initialized the Quill editor to include the new dictation toolbar handlers and render the custom icons for the English and Urdu recording buttons.

### 2.3. Technologies & Libraries Used
- **Web Standard:** Browser Native Web Speech API (`SpeechRecognition` / `webkitSpeechRecognition`)
- **Rich Text Editor:** Quill.js
- **Natural Language Processing:** Compromise NLP (`window.nlp`) for client-side English noun extraction.

### 2.4. The "Supervisor Explanation" (Executive Summary)
> **Audio Dictation / Transcription Feature**
> "The Audio Dictation feature modernizes data entry for the Minute Sheet app, letting users draft long descriptions using just their voice. Supporting both English and Urdu, it taps right into the browser’s native speech recognition—meaning we achieve high-quality voice-to-text without incurring costly third-party transcription fees. It continuously listens and transcribes in real-time, pulling out key nouns to help structure the document. This vastly improves accessibility, speeds up the sheet creation process, and accommodates multilingual staff who might prefer speaking over typing."
