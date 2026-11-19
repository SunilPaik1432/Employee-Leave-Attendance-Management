for (const start of document.querySelectorAll('input[name="start"]')) {
    const end = start.form.elements.namedItem('end');
    start.addEventListener('change', () => {
        end.min = start.value;
        if (end.value && end.value < start.value) end.value = '';
    });
}