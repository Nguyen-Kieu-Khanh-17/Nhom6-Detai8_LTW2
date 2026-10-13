const params = new URLSearchParams(window.location.search);
const message = document.querySelector('#message');

function showError(text) { message.textContent = text; }

async function submitJson(url, payload) {
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(data.message || 'Không thể thực hiện yêu cầu.');
  return data;
}

const loginForm = document.querySelector('#login-form');
if (loginForm) {
  loginForm.addEventListener('submit', async event => {
    event.preventDefault();
    try {
      const user = await submitJson('/api/auth/login', {
        username: document.querySelector('#username').value,
        password: document.querySelector('#password').value
      });
      const destinations = { Admin: '/admin.html', Doctor: '/doctor.html', User: '/user.html', Support: '/support.html' };
      window.location.href = destinations[user.role] || '/';
    } catch (error) { showError(error.message); }
  });
}

const registerForm = document.querySelector('#register-form');
if (registerForm) {
  registerForm.addEventListener('submit', async event => {
    event.preventDefault();
    try {
      await submitJson('/api/auth/register', {
        fullName: document.querySelector('#full-name').value,
        email: document.querySelector('#email').value,
        dateOfBirth: document.querySelector('#date-of-birth').value,
        phone: document.querySelector('#phone').value,
        address: document.querySelector('#address').value,
        username: document.querySelector('#username').value,
        password: document.querySelector('#password').value
      });
      window.location.href = '/login.html?registered=1';
    } catch (error) { showError(error.message); }
  });
}

if (params.get('registered')) showError('Đăng ký thành công. Hãy đăng nhập để tiếp tục.');
