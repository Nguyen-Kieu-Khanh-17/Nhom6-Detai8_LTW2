loadCurrentUser().then(user => {
  if (user) {
    const profileName = document.querySelector('#profile-name');
    const profileRole = document.querySelector('#profile-role');
    if (profileName) profileName.textContent = user.fullName;
    if (profileRole) profileRole.textContent = user.role;
  }
});
