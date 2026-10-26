(function () {
    var accountForm = document.querySelector('.account-create-form');
    if (accountForm) {
        var feedback = accountForm.querySelector('.account-form-feedback');
        var submitButton = accountForm.querySelector('button[type="submit"]');
        var originalButtonText = submitButton.textContent;

        function clearAccountErrors() {
            accountForm.querySelectorAll('[data-valmsg-for]').forEach(function (message) { message.textContent = ''; });
            accountForm.querySelectorAll('.input-validation-error').forEach(function (field) {
                field.classList.remove('input-validation-error');
                field.removeAttribute('aria-invalid');
            });
            feedback.textContent = '';
            feedback.hidden = true;
            feedback.classList.remove('is-error', 'is-success');
        }

        function showFieldError(name, message) {
            var field = accountForm.elements[name];
            var target = accountForm.querySelector('[data-valmsg-for="' + name + '"]');
            if (target) target.textContent = message;
            if (field && field.length && !field.tagName) field = field[0];
            if (field && field.classList) {
                field.classList.add('input-validation-error');
                field.setAttribute('aria-invalid', 'true');
            }
        }

        accountForm.querySelectorAll('input, select').forEach(function (field) {
            function clearFieldError() {
                var target = accountForm.querySelector('[data-valmsg-for="' + field.name + '"]');
                if (target && target.textContent) {
                    target.textContent = '';
                    field.classList.remove('input-validation-error');
                    field.removeAttribute('aria-invalid');
                }
            }
            field.addEventListener('input', clearFieldError);
            field.addEventListener('change', clearFieldError);
        });

        accountForm.addEventListener('submit', function (event) {
            event.preventDefault();
            clearAccountErrors();

            var firstInvalid = null;
            accountForm.querySelectorAll('input, select').forEach(function (field) {
                if (!field.willValidate || field.validity.valid) return;
                var message = field.validationMessage || 'Please check your entries.';
                if (field.name === 'Password' && field.validity.patternMismatch)
                    message = 'Use at least 8 characters, including upper and lowercase letters, a number, and a symbol.';
                showFieldError(field.name, message);
                firstInvalid = firstInvalid || field;
            });

            var password = accountForm.elements.Password;
            var confirmation = accountForm.elements.ConfirmPassword;
            if (!firstInvalid && password.value !== confirmation.value) {
                showFieldError('ConfirmPassword', 'The passwords do not match.');
                firstInvalid = confirmation;
            }

            var dob = accountForm.elements.DateOfBirth;
            if (dob.value) {
                var now = new Date();
                var today = new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
                if (dob.value > today) {
                    showFieldError('DateOfBirth', 'Date of birth cannot be in the future.');
                    firstInvalid = firstInvalid || dob;
                }
            }

            if (firstInvalid) {
                firstInvalid.focus();
                return;
            }

            submitButton.disabled = true;
            submitButton.textContent = 'Creating…';
            $.ajax({
                url: accountForm.action,
                type: accountForm.method || 'POST',
                data: $(accountForm).serialize(),
                dataType: 'json'
            }).done(function (response) {
                feedback.textContent = response.message || 'Account created.';
                feedback.hidden = false;
                feedback.classList.add('is-success');
                window.location.reload();
            }).fail(function (xhr) {
                var response = xhr.responseJSON;
                var errors = response && response.errors;
                var firstError = null;
                if (errors) {
                    Object.keys(errors).forEach(function (name) {
                        var messages = errors[name] || [];
                        if (name && accountForm.querySelector('[data-valmsg-for="' + name + '"]')) {
                            showFieldError(name, messages.join(' '));
                            firstError = firstError || accountForm.elements[name];
                        } else if (messages.length) {
                            feedback.textContent += (feedback.textContent ? ' ' : '') + messages.join(' ');
                        }
                    });
                }
                if (!feedback.textContent) feedback.textContent = 'Please check your entries. If the issue persists, try again.';
                feedback.hidden = false;
                feedback.classList.add('is-error');
                if (firstError && firstError.focus) firstError.focus();
            }).always(function () {
                submitButton.disabled = false;
                submitButton.textContent = originalButtonText;
            });
        });
    }

    function bindFilter(inputId, tableSelector) {
        var input = document.getElementById(inputId);
        if (!input) return;
        input.addEventListener('input', function () {
            var query = input.value.toLocaleLowerCase();
            input.closest('.panel').querySelectorAll(tableSelector + ' tbody tr').forEach(function (row) {
                row.hidden = row.textContent.toLocaleLowerCase().indexOf(query) === -1;
            });
        });
    }
    bindFilter('employeeSearch', '.app-table');
    var taskFilter = 'all';
    var taskSearch = document.getElementById('taskSearch');
    function applyTaskFilter() {
        if (!taskSearch) return;
        var query = taskSearch.value.toLocaleLowerCase();
        document.querySelectorAll('.app-table tbody tr[data-status]').forEach(function (row) {
            var statusMatches = taskFilter === 'all' || (taskFilter === 'overdue' ? row.dataset.overdue === 'true' : row.dataset.status === taskFilter);
            row.hidden = !statusMatches || row.textContent.toLocaleLowerCase().indexOf(query) === -1;
        });
    }
    if (taskSearch) taskSearch.addEventListener('input', applyTaskFilter);
    document.querySelectorAll('[data-task-filter]').forEach(function (link) {
        link.addEventListener('click', function (event) {
            event.preventDefault();
            taskFilter = link.dataset.taskFilter;
            document.querySelectorAll('[data-task-filter]').forEach(function (item) { item.classList.toggle('active', item === link); });
            applyTaskFilter();
        });
    });
    document.querySelectorAll('.project-report-form').forEach(function (form) {
        var task = form.querySelector('.report-task-select');
        var complete = form.querySelector('.report-complete-select');
        var status = form.querySelector('.report-progress-status');
        var percent = form.querySelector('.report-percent');
        var note = form.querySelector('.report-note');
        function syncReportFields() {
            var taskReport = !!task.value;
            if (!taskReport) complete.value = '';
            complete.required = taskReport;
            var isComplete = taskReport && complete.value === 'true';
            status.disabled = !taskReport || isComplete;
            percent.disabled = !taskReport || isComplete;
            note.required = !taskReport || complete.value === 'false';
            note.placeholder = taskReport ? (isComplete ? 'Optional project suggestion' : 'Explain why the task is not complete') : 'Project suggestion';
        }
        task.addEventListener('change', syncReportFields);
        complete.addEventListener('change', syncReportFields);
        syncReportFields();
    });
}());