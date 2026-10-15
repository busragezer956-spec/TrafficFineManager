document.querySelectorAll('form[data-confirm]').forEach(form => {
  form.addEventListener('submit', event => { if (!window.confirm(form.dataset.confirm)) event.preventDefault(); });
});
document.querySelectorAll('form[data-decision]').forEach(form => {
  const note = form.querySelector('[name="Note"]');
  form.querySelectorAll('button[name="Action"]').forEach(button => {
    button.addEventListener('click', () => {
      note.setCustomValidity('');
      note.required = button.value === 'Rejected';
      if (note.required && !note.value.trim()) note.setCustomValidity('Ret nedeni zorunludur.');
    });
  });
  note.addEventListener('input', () => note.setCustomValidity(''));
});
