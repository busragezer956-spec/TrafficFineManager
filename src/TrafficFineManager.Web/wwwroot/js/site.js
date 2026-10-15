document.querySelectorAll('form[data-confirm]').forEach(form => {
  form.addEventListener('submit', event => { if (!window.confirm(form.dataset.confirm)) event.preventDefault(); });
});
document.querySelectorAll('form[data-decision]').forEach(form => {
  form.addEventListener('submit', event => {
    const note = form.querySelector('[name="Note"]');
    note.setCustomValidity('');
    if (event.submitter?.value === 'Rejected' && !note.value.trim()) {
      event.preventDefault(); note.setCustomValidity('Ret nedeni zorunludur.'); note.reportValidity();
    }
  });
  form.querySelector('[name="Note"]').addEventListener('input', event => event.target.setCustomValidity(''));
});
