(() => {
    const form = document.getElementById('submitForm');
    const argumentText = document.getElementById('argumentText');
    const guidedDraft = document.getElementById('guidedDraft');
    const counter = document.getElementById('charCount');
    const submitButton = document.getElementById('submitBtn');
    const directButton = document.getElementById('directModeButton');
    const guidedButton = document.getElementById('guidedModeButton');
    const directPanel = document.getElementById('directMode');
    const guidedPanel = document.getElementById('guidedMode');
    const description = document.getElementById('modeDescription');
    const input = document.getElementById('sensemakingInput');
    const sendButton = document.getElementById('sendSensemaking');
    const guidedSubmit = document.getElementById('submitGuided');
    const log = document.getElementById('conversationLog');
    const error = document.getElementById('sensemakingError');
    const status = document.getElementById('draftStatus');
    const token = form.querySelector('input[name="__RequestVerificationToken"]').value;
    const storageKey = 'argument-sensemaking-session';
    const messages = [];
    let activeMode = 'direct';
    let waiting = false;

    function saveSession() {
        try {
            localStorage.setItem(storageKey, JSON.stringify({
                messages,
                draft: guidedDraft.value,
                mode: activeMode
            }));
        } catch {
            // The workflow remains usable when browser storage is unavailable.
        }
    }

    function restoreSession() {
        try {
            const session = JSON.parse(localStorage.getItem(storageKey));
            if (!session || !Array.isArray(session.messages)) return;

            session.messages.forEach(message => {
                if ((message.role === 'user' || message.role === 'assistant') && typeof message.content === 'string') {
                    messages.push(message);
                    appendMessage(message.role, message.content);
                }
            });
            guidedDraft.value = typeof session.draft === 'string' ? session.draft : '';
            if (session.mode === 'guided') setMode('guided');
        } catch {
            localStorage.removeItem(storageKey);
        }
    }

    function setMode(mode) {
        activeMode = mode;
        const guided = mode === 'guided';
        directPanel.classList.toggle('d-none', guided);
        guidedPanel.classList.toggle('d-none', !guided);
        directButton.classList.toggle('active', !guided);
        guidedButton.classList.toggle('active', guided);
        directButton.setAttribute('aria-selected', String(!guided));
        guidedButton.setAttribute('aria-selected', String(guided));
        description.textContent = guided
            ? 'Begin with uncertainty. A reflection partner will help you find the claim, values, and assumptions underneath it.'
            : 'Share a fully formed argument for analysis and mapping.';
        if (guided) input.focus(); else argumentText.focus();
        saveSession();
    }

    function appendMessage(role, content) {
        document.getElementById('conversationEmpty')?.remove();
        const row = document.createElement('div');
        row.className = `chat-row ${role}`;
        const bubble = document.createElement('div');
        bubble.className = 'chat-bubble';
        bubble.textContent = content;
        row.appendChild(bubble);
        log.appendChild(row);
        log.scrollTop = log.scrollHeight;
        return row;
    }

    function updateDraftState(ready = false) {
        const longEnough = guidedDraft.value.trim().length >= 30;
        guidedSubmit.disabled = !longEnough || waiting;
        status.textContent = ready ? 'Ready to submit' : (longEnough ? 'Taking shape' : 'Listening');
        status.classList.toggle('ready', ready);
    }

    async function sendMessage() {
        const content = input.value.trim();
        if (!content || waiting) return;

        waiting = true;
        error.classList.add('d-none');
        messages.push({ role: 'user', content });
        const userRow = appendMessage('user', content);
        input.value = '';
        sendButton.disabled = true;
        sendButton.innerHTML = '<span class="spinner-border spinner-border-sm" role="status"></span>';
        updateDraftState();

        try {
            let response;
            for (let attempt = 0; attempt < 2; attempt++) {
                response = await fetch(window.argumentSensemakingUrl, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                    body: JSON.stringify({ messages, currentDraft: guidedDraft.value })
                });
                if (response.status !== 503 || attempt === 1) break;
                await new Promise(resolve => setTimeout(resolve, 1500));
            }

            const responseText = await response.text();
            let result;
            try {
                result = JSON.parse(responseText);
            } catch {
                throw new Error('The reflection partner returned an unreadable response. Please try again.');
            }
            if (!response.ok) throw new Error(result.error || 'The reflection partner could not respond.');

            messages.push({ role: 'assistant', content: result.reply });
            appendMessage('assistant', result.reply);
            guidedDraft.value = result.draft || guidedDraft.value;
            updateDraftState(result.ready);
            saveSession();
        } catch (requestError) {
            messages.pop();
            userRow.remove();
            input.value = content;
            error.textContent = requestError.message;
            error.classList.remove('d-none');
        } finally {
            waiting = false;
            sendButton.disabled = false;
            sendButton.innerHTML = '<i class="bi bi-arrow-up"></i>';
            updateDraftState(status.classList.contains('ready'));
            input.focus();
        }
    }

    directButton.addEventListener('click', () => setMode('direct'));
    guidedButton.addEventListener('click', () => setMode('guided'));
    document.querySelectorAll('.prompt-chip').forEach(chip => chip.addEventListener('click', () => {
        input.value = chip.textContent;
        input.focus();
    }));
    sendButton.addEventListener('click', sendMessage);
    input.addEventListener('keydown', event => {
        if (event.key === 'Enter' && !event.shiftKey) {
            event.preventDefault();
            sendMessage();
        }
    });
    guidedDraft.addEventListener('input', () => {
        updateDraftState();
        saveSession();
    });
    argumentText.addEventListener('input', () => {
        counter.textContent = `${argumentText.value.length.toLocaleString()} characters`;
    });
    guidedSubmit.addEventListener('click', () => {
        argumentText.value = guidedDraft.value.trim();
        localStorage.removeItem(storageKey);
        form.requestSubmit();
    });
    form.addEventListener('submit', event => {
        if (activeMode === 'guided') argumentText.value = guidedDraft.value.trim();
        if (argumentText.value.trim().length < 30) {
            event.preventDefault();
            setMode(activeMode);
            return;
        }
        submitButton.disabled = true;
        guidedSubmit.disabled = true;
        submitButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status"></span>Submitting...';
    });

    counter.textContent = `${argumentText.value.length.toLocaleString()} characters`;
    restoreSession();
    updateDraftState();
})();
