import { enhance } from 'https://unpkg.com/aurelia-script@1.5.2/dist/aurelia.esm.min.js'

class LoginFormViewModel {

  constructor() {
    this.email = "";
    this.password = "";
  }

  async submit() {
    try {
      const form = document.querySelector('[name=loginform]');
      const token = getCSRFTokenFromForm(form);
      const result =
        await fetch("/auth/login", {
          body: JSON.stringify({ email: this.email, password: this.password }),
          method: 'POST',
          headers: {
            Accept: 'application/json',
            RequestVerificationToken: token,
            'Content-Type': 'application/json',
          }
        }).then(res => res.json())
      console.log(result);
      location.reload();
    } catch (error) {
      console.error({ error });
    }
  }

}

class SignupViewModel {

  constructor() {
    this.emailExists = false;
    this.isValidPassword = false;
    this.email = "";
    this.password = "";
    this.repeatPassword = "";
    this.name = "";
    this.lastName = "";
    this.invite = "";
  }

  async submit() {
    try {
      const form = document.querySelector('[name=signupform]');
      const token = getCSRFTokenFromForm(form);
      const body =
        JSON.stringify({
          email: this.email,
          password: this.password,
          name: this.name,
          lastName: this.lastName,
          invite: this.invite,
        });
      const result =
        await fetch("/auth/signup", {
          body,
          headers: {
            Accept: "application/json",
            RequestVerificationToken: token,
            "content-type": 'application/json',
          },
          method: 'POST',
        }).then(res => res.json())
      console.log(result);
      location.reload();
    } catch (error) {
      console.error({ error });
    }
  }

  async checkExists() {
    const result =
      await fetch("/auth/exists", {
        body: JSON.stringify({ email: this.email }),
        headers: {
          Accept: "application/json",
          "content-type": 'application/json',
        },
        method: 'POST',
      }).then(res => res.json())
    this.emailExists = result.exists;
  }

  checkPassword() {
    this.isValidPassword = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[a-zA-Z\d]{8,}$/.test(this.password) && this.password === this.repeatPassword;
  }

}


enhance({
  host: document.querySelector('.loginsection'),
  root: LoginFormViewModel
});

enhance({
  host: document.querySelector('.signupsection'),
  root: SignupViewModel
});

/**
 * 
 * @param {HTMLFormElement} form 
 */
function getCSRFTokenFromForm(form) {
  const tokenInput = form.querySelector('input[name=__RequestVerificationToken]');
  const token = tokenInput?.value;
  return token;
}
