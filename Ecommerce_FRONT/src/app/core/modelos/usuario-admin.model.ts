// Respuestas de /api/auth/usuarios y /api/auth/roles (solo Administrator).
export interface UsuarioResumen {
  userId: string;
  nombre: string;
  apellido: string;
  email: string;
  activo: boolean;
  roles: string[];
  bloqueadoHasta: string | null;
  createdAt: string;
  ultimoAcceso: string | null;
}

export interface EventoAuditoria {
  tipo: string;
  exitoso: boolean;
  ip: string | null;
  detalle: string | null;
  fecha: string;
}

export interface UsuarioDetalle extends UsuarioResumen {
  intentosFallidos: number;
  actividad: EventoAuditoria[];
}

export interface RolInfo {
  nombre: string;
  descripcion: string | null;
  usuarios: number;
}

export interface CrearUsuarioPeticion {
  nombre: string;
  apellido: string;
  email: string;
  password: string;
  roles: string[];
}

// Eventos que registra Auth en AuthAuditLogs, en lenguaje del panel.
export const EVENTOS_AUDITORIA: Record<string, { texto: string; icono: string; clase: string }> = {
  LoginSucceeded: { texto: 'Inició sesión', icono: 'bi-box-arrow-in-right', clase: 'exito' },
  LoginFailed: { texto: 'Contraseña incorrecta', icono: 'bi-x-circle', clase: 'peligro' },
  AccountLocked: { texto: 'Cuenta bloqueada por intentos fallidos', icono: 'bi-lock-fill', clase: 'peligro' },
  LoginLockedOut: { texto: 'Intento con la cuenta bloqueada', icono: 'bi-lock', clase: 'peligro' },
  LoginInactiveUser: { texto: 'Intento con la cuenta desactivada', icono: 'bi-slash-circle', clase: 'peligro' },
  Logout: { texto: 'Cerró sesión', icono: 'bi-box-arrow-right', clase: 'neutro' },
  TokenRefreshed: { texto: 'Renovó la sesión', icono: 'bi-arrow-repeat', clase: 'neutro' },
  RefreshTokenReuseDetected: { texto: 'Reuso de token detectado: sesiones revocadas', icono: 'bi-shield-exclamation', clase: 'peligro' },
  UserRegistered: { texto: 'Se registró en la tienda', icono: 'bi-person-plus', clase: 'info' },
  PasswordChanged: { texto: 'Cambió su contraseña', icono: 'bi-key', clase: 'info' },
  PasswordChangeFailed: { texto: 'Falló el cambio de contraseña', icono: 'bi-key', clase: 'peligro' },
  UserCreatedByAdmin: { texto: 'Creado desde el panel', icono: 'bi-person-plus', clase: 'primario' },
  RolesChanged: { texto: 'Cambio de roles', icono: 'bi-person-gear', clase: 'primario' },
  UserActivated: { texto: 'Cuenta activada', icono: 'bi-person-check', clase: 'primario' },
  UserDeactivated: { texto: 'Cuenta desactivada', icono: 'bi-person-x', clase: 'primario' },
  UserUnlocked: { texto: 'Cuenta desbloqueada', icono: 'bi-unlock', clase: 'primario' }
};
