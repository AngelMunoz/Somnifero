class LoginFormViewModel {

  constructor() {
    this.email = "";
    this.password = "";
  }

  async submit() {
    const result =
      await fetch("/auth/login", {
        body: JSON.stringify({ email: this.email, password: this.password }),
        method: 'POST',
        headers: {
          Accept: 'application/json',
          'Content-Type': 'application/json'
        }
      }).then(res => res.json())
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

  }

  checkPassword() {
    this.isValidPassword = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[a-zA-Z\d]{8,}$/.test(this.password) && this.password === this.repeatPassword;
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

}


au
  .enhance({
    host: document.querySelector('.loginsection'),
    root: LoginFormViewModel
  });

au
  .enhance({
    host: document.querySelector('.signupsection'),
    root: SignupViewModel
  });