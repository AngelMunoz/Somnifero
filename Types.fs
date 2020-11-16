namespace Somnifero.Types

type AuthResponse = { User: string }

type LoginPayload = { email: string; password: string }

type SignUpPayload =
    { email: string
      password: string
      name: string
      lastName: string
      invite: string }

type User =
    { name: string
      lastName: string
      email: string }