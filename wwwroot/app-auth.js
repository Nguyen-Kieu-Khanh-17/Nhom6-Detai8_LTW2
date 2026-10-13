async function loadCurrentUser() {
  const response = await fetch('/api/auth/me');
  if (!response.ok) {
    window.location.href = '/login.html';
    return null;
  }
  return response.json();
}

document.querySelector('#logout-button')?.addEventListener('click', async () => {
  await fetch('/api/auth/logout', { method: 'POST' });
  window.location.href = '/login.html';
});
