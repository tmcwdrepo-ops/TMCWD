// login.js — login page script for the ASP.NET MVC Razor view.
document.addEventListener('DOMContentLoaded', () => {

  const form = document.getElementById('login-form');
  const emailInput = document.getElementById('email');
  const passwordInput = document.getElementById('password');
  const emailError = document.getElementById('email-error');
  const passwordError = document.getElementById('password-error');
  const togglePwBtn = document.getElementById('toggle-pw');
  const eyeIcon = document.getElementById('eye-icon');
  const signinBtn = document.getElementById('signin-btn');
  const btnLabel = signinBtn.querySelector('.btn-label');
  const spinner = document.getElementById('spinner');
  const rememberCheckbox = document.getElementById('remember');
  const logoImg = document.getElementById('logo-img');
  const logoFallback = document.getElementById('logo-fallback');

  // --- Logo placeholder: show the fallback icon until a real logo loads ---
  logoImg.addEventListener('error', () => {
    logoImg.style.display = 'none';
    logoFallback.style.display = 'flex';
  });

  logoImg.addEventListener('load', () => {
    logoFallback.style.display = 'none';
  });

  const EYE_OPEN = `<path d="M1 12s4-7 11-7 11 7 11 7-4 7-11 7-11-7-11-7Z" stroke="currentColor" stroke-width="1.6"/><circle cx="12" cy="12" r="3" stroke="currentColor" stroke-width="1.6"/>`;
  const EYE_CLOSED = `<path d="M3 3l18 18" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/><path d="M10.6 5.2A11 11 0 0 1 12 5c7 0 11 7 11 7a13.5 13.5 0 0 1-3.6 4.1M6.6 6.6C3.6 8.3 1 12 1 12s4 7 11 7c1.4 0 2.7-.2 3.9-.6" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/>`;

  // --- Password visibility toggle ---
  togglePwBtn.addEventListener('click', () => {
    const isPassword = passwordInput.type === 'password';
    passwordInput.type = isPassword ? 'text' : 'password';
    eyeIcon.innerHTML = isPassword ? EYE_CLOSED : EYE_OPEN;
    togglePwBtn.setAttribute('aria-label', isPassword ? 'Hide password' : 'Show password');
  });

  // --- Clear inline errors as the user types ---
  emailInput.addEventListener('input', () => clearFieldError(emailInput, emailError));
  passwordInput.addEventListener('input', () => clearFieldError(passwordInput, passwordError));

  function clearFieldError(input, errorEl) {
    input.closest('.field').classList.remove('has-error');
    errorEl.textContent = '';
  }

  function setFieldError(input, errorEl, message) {
    input.closest('.field').classList.add('has-error');
    errorEl.textContent = message;
  }

  function isValidEmail(value) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
  }

  // --- Form validation + submit handling ---
  form.addEventListener('submit', (event) => {
    event.preventDefault();

    let hasError = false;
    const emailValue = emailInput.value.trim();
    const passwordValue = passwordInput.value;

    if (!emailValue) {
      setFieldError(emailInput, emailError, 'Email address is required.');
      hasError = true;
    } else if (!isValidEmail(emailValue)) {
      setFieldError(emailInput, emailError, 'Enter a valid email address.');
      hasError = true;
    } else {
      clearFieldError(emailInput, emailError);
    }

    if (!passwordValue) {
      setFieldError(passwordInput, passwordError, 'Password is required.');
      hasError = true;
    } else {
      clearFieldError(passwordInput, passwordError);
    }

    if (hasError) return;

    // Show loading state then submit the form
    setLoading(true);
    form.submit();
  });

  function setLoading(isLoading) {
    signinBtn.disabled = isLoading;
    spinner.hidden = !isLoading;
    btnLabel.textContent = isLoading ? 'Signing in…' : 'Sign In';
  }

});
