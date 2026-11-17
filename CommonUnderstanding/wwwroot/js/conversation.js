/**
 * Conversation Shell — client-side behaviour.
 *
 * Handles:
 *  - Message sending via fetch to /api/conversation/send
 *  - Streaming agent responses via SignalR (ConversationHub)
 *  - Auto-resizing textarea
 *  - Graph overlay toggle
 */

(function () {
    'use strict';

    const form = document.getElementById('conversationForm');
    const input = document.getElementById('messageInput');
    const thread = document.getElementById('conversationThread');
    const sendBtn = form?.querySelector('.cu-send-btn');

    if (!form || !input || !thread) return;

    let isSending = false;

    // ── Auto-resize textarea ──────────────────────────────────────────
    input.addEventListener('input', function () {
        this.style.height = 'auto';
        this.style.height = Math.min(this.scrollHeight, 150) + 'px';
    });

    // ── Submit handler ────────────────────────────────────────────────
    form.addEventListener('submit', async function (e) {
        e.preventDefault();

        const message = input.value.trim();
        if (!message || isSending) return;

        isSending = true;
        sendBtn.disabled = true;

        // Render user message immediately
        appendMessage('user', message);
        input.value = '';
        input.style.height = 'auto';

        // Show typing indicator
        const typingId = showTyping();

        try {
            const resp = await fetch('/api/conversation/send', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify({ message: message })
            });

            removeTyping(typingId);

            if (!resp.ok) {
                appendMessage('agent', 'I had trouble processing that. Could you try again?');
                return;
            }

            const data = await resp.json();
            appendMessage('agent', data.response);

        } catch (err) {
            removeTyping(typingId);
            appendMessage('agent', 'Something went wrong on my end. Please try again.');
            console.error('Conversation error:', err);
        } finally {
            isSending = false;
            sendBtn.disabled = false;
            input.focus();
        }
    });

    // ── Enter to send (Shift+Enter for newline) ───────────────────────
    input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            form.dispatchEvent(new Event('submit', { cancelable: true }));
        }
    });

    // ── Helpers ───────────────────────────────────────────────────────

    function appendMessage(role, text) {
        const isUser = role === 'user';
        const el = document.createElement('div');
        el.className = 'cu-message cu-message--' + role;
        el.innerHTML =
            '<div class="cu-message-avatar">' +
                (isUser ? '<i class="bi bi-person-fill"></i>' : '<i class="bi bi-robot"></i>') +
            '</div>' +
            '<div>' +
                '<div class="cu-message-bubble">' + escapeHtml(text) + '</div>' +
                '<div class="cu-message-time">' + formatTime(new Date()) + '</div>' +
            '</div>';
        thread.appendChild(el);
        thread.scrollTop = thread.scrollHeight;
    }

    function showTyping() {
        const id = 'typing-' + Date.now();
        const el = document.createElement('div');
        el.id = id;
        el.className = 'cu-message cu-message--agent';
        el.innerHTML =
            '<div class="cu-message-avatar"><i class="bi bi-robot"></i></div>' +
            '<div class="cu-message-bubble">' +
                '<div class="cu-typing-indicator"><span></span><span></span><span></span></div>' +
            '</div>';
        thread.appendChild(el);
        thread.scrollTop = thread.scrollHeight;
        return id;
    }

    function removeTyping(id) {
        const el = document.getElementById(id);
        if (el) el.remove();
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function formatTime(date) {
        return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    }

    function getAntiForgeryToken() {
        const meta = document.querySelector('input[name="__RequestVerificationToken"]');
        return meta ? meta.value : '';
    }
})();