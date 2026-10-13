// Client-side counterparts of server rules. The server always re-validates; these only give fast feedback.
(function ($) {
    if ($ && $.validator && $.validator.unobtrusive) {
        // [DateAfter("StartDate")] -> data-val-dateafter / data-val-dateafter-other
        $.validator.addMethod('dateafter', function (value, element, otherSelector) {
            if (!value) return true;
            var otherValue = $(otherSelector).val();
            if (!otherValue) return true;
            return new Date(value) > new Date(otherValue);
        });
        $.validator.unobtrusive.adapters.add('dateafter', ['other'], function (options) {
            var prefix = options.element.name.substr(0, options.element.name.lastIndexOf('.') + 1);
            var otherName = prefix + options.params.other;
            options.rules.dateafter = $(options.form).find(':input[name="' + otherName + '"]');
            options.messages.dateafter = options.message;
        });
    }
})(window.jQuery);

// Signed agreement upload: PDF only, max 10 MB.
document.querySelectorAll('[data-pdf-upload]').forEach(function (form) {
    form.addEventListener('submit', function (e) {
        var input = form.querySelector('input[type=file]');
        var error = form.querySelector('[data-pdf-error]');
        var file = input.files && input.files[0];
        var message = '';
        if (!file) message = 'Select a PDF file to upload.';
        else if (!/\.pdf$/i.test(file.name)) message = 'Only PDF files are allowed.';
        else if (file.size > 10 * 1024 * 1024) message = 'The file is too large. The maximum size is 10 MB.';
        if (message) {
            e.preventDefault();
            error.textContent = message;
        }
    });
});

// Live USD -> ZAR preview on the service request form. The saved value is recalculated on the server.
document.querySelectorAll('[data-zar-preview]').forEach(function (output) {
    var rate = parseFloat(output.getAttribute('data-rate'));
    var input = document.getElementById(output.getAttribute('data-source'));
    if (!input || !(rate > 0)) return;
    var update = function () {
        var usd = parseFloat(input.value);
        output.textContent = usd > 0
            ? 'R ' + (Math.round(usd * rate * 100) / 100).toLocaleString('en-ZA', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
            : '-';
    };
    input.addEventListener('input', update);
    update();
});
