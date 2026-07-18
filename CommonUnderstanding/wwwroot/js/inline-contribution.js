/**
 * Inline "What do you think?" contribution — shared JavaScript.
 * Used by _InlineContribution.cshtml partial and all views that embed it.
 */
(function () {
    'use strict';

    // ── Toggle inline contribution form ──────────────────────────────────
    window.toggleInlineContribution = function (contextId) {
        const form = document.getElementById('inline-form-' + contextId);
        const chevron = document.getElementById('inline-chevron-' + contextId);
        const trigger = document.getElementById('inline-trigger-' + contextId);
        const textarea = document.getElementById('inline-textarea-' + contextId);

        if (!form || !chevron) return;

        const isHidden = form.style.display === 'none';
        form.style.display = isHidden ? 'block' : 'none';
        chevron.className = isHidden
            ? 'bi bi-chevron-up ms-auto'
            : 'bi bi-chevron-down ms-auto';

        if (trigger) {
            trigger.style.borderStyle = isHidden ? 'solid' : 'dashed';
        }

        if (isHidden && textarea) {
            textarea.focus();
        }
    };

    // ── Submit inline contribution ───────────────────────────────────────
    window.submitInlineContribution = async function (contextId, event) {
        if (event) event.preventDefault();

        const form = document.getElementById('inlineContributionForm-' + contextId);
        const textarea = document.getElementById('inline-textarea-' + contextId);
        const submitBtn = document.getElementById('inline-submit-btn-' + contextId);
        const spinner = document.getElementById('inline-spinner-' + contextId);
        const resultDiv = document.getElementById('inline-result-' + contextId);

        if (!form || !textarea || !submitBtn) return false;

        const text = textarea.value.trim();
        if (!text) {
            showInlineResult(contextId, 'Please enter your perspective before submitting.', 'warning');
            return false;
        }

        // Disable form
        submitBtn.disabled = true;
        if (spinner) spinner.classList.remove('d-none');
        if (resultDiv) resultDiv.style.display = 'none';

        try {
            const formData = new FormData(form);
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

            const response = await fetch('/Argument/SubmitInline', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token,
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    argumentText: text,
                    contextType: formData.get('contextType'),
                    contextId: formData.get('contextId'),
                    contextLabel: formData.get('contextLabel')
                })
            });

            if (!response.ok) {
                const errData = await response.json().catch(() => ({}));
                throw new Error(errData.message || 'Submission failed. Please try again.');
            }

            const data = await response.json();

            // Show success and redirect to analysis page
            showInlineResult(contextId,
                '<i class="bi bi-check-circle-fill text-success me-1"></i>' +
                '<strong>Submitted!</strong> Your perspective is being analyzed. ' +
                '<a href="' + (data.analyzeUrl || '/Argument/Analyze/' + data.argumentId) + '" class="alert-link">View analysis →</a>',
                'success');

            // Clear the textarea
            textarea.value = '';
            const counter = document.getElementById('inline-charcount-' + contextId);
            if (counter) counter.textContent = '0';

            // Collapse the form after a short delay
            setTimeout(() => {
                toggleInlineContribution(contextId);
            }, 3000);

        } catch (error) {
            console.error('Inline contribution failed:', error);
            showInlineResult(contextId,
                '<i class="bi bi-exclamation-triangle-fill text-danger me-1"></i>' +
                (error.message || 'Something went wrong. Please try again.'),
                'danger');
        } finally {
            submitBtn.disabled = false;
            if (spinner) spinner.classList.add('d-none');
        }

        return false;
    };

    // ── Show result message ──────────────────────────────────────────────
    function showInlineResult(contextId, message, type) {
        const resultDiv = document.getElementById('inline-result-' + contextId);
        if (!resultDiv) return;

        const alertClass = type === 'success' ? 'alert-success'
            : type === 'warning' ? 'alert-warning'
            : 'alert-danger';

        resultDiv.innerHTML = '<div class="alert ' + alertClass + ' py-2 px-3 mb-0 small">' + message + '</div>';
        resultDiv.style.display = 'block';

        // Auto-hide success messages after 8 seconds
        if (type === 'success') {
            setTimeout(() => {
                resultDiv.style.display = 'none';
            }, 8000);
        }
    }
})();