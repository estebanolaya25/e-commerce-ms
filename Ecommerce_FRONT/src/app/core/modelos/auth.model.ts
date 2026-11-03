export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegistroRequest {
  nombre: string;
  apellido: string;
  email: string;
  password: string;
}

// Respuesta de /api/auth/login, /registro y /refresh.
export interface AuthResponse {
  access_token: string;
  refresh_token: string;
  expiration: string;
  userId: string;
  user: string;
  roles: string[];
}

// Respuesta de /api/auth/current.
export interface UsuarioActual {
  idUsuario: string;
  nombre: string;
  apellido: string;
  email: string;
  emailVerificado: boolean;
  roles: string[];
}

// Datos del usuario que se leen del propio JWT (sin llamar al backend).
export interface UsuarioSesion {
  id: string;
  email: string;
  nombre: string;
  apellido: string;
  roles: string[];
  expiraEn: number;
}
