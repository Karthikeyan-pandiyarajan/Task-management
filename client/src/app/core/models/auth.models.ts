// Mirrors Application.Auth DTOs in the .NET API.

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  token: string;
  email: string;
}

export interface Session {
  token: string;
  email: string;
}
