import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { AuthResponse, UsuarioSesion } from '../modelos/auth.model';

const CLAVE_ACCESS = 'tienda.accessToken';
const CLAVE_REFRESH = 'tienda.refreshToken';

// Guarda los tokens en localStorage y expone el usuario actual como observable.
// No hace llamadas HTTP: así el interceptor puede usarlo sin dependencias circulares.
@Injectable({ providedIn: 'root' })
export class SesionService {
  private readonly usuarioSubject = new BehaviorSubject<UsuarioSesion | null>(this.leerUsuario());
  readonly usuario$: Observable<UsuarioSesion | null> = this.usuarioSubject.asObservable();

  constructor() {
    // Si el usuario inicia o cierra sesión en otra pestaña, esta se entera.
    window.addEventListener('storage', evento => {
      if (evento.key === CLAVE_ACCESS || evento.key === CLAVE_REFRESH) {
        this.usuarioSubject.next(this.leerUsuario());
      }
    });
  }

  get usuario(): UsuarioSesion | null {
    return this.usuarioSubject.value;
  }

  get accessToken(): string | null {
    return localStorage.getItem(CLAVE_ACCESS);
  }

  get refreshToken(): string | null {
    return localStorage.getItem(CLAVE_REFRESH);
  }

  // Con refresh token la sesión sigue viva aunque el access token haya vencido: el interceptor lo renueva.
  get estaAutenticada(): boolean {
    return this.refreshToken !== null;
  }

  guardar(respuesta: AuthResponse): void {
    localStorage.setItem(CLAVE_ACCESS, respuesta.access_token);
    localStorage.setItem(CLAVE_REFRESH, respuesta.refresh_token);
    this.usuarioSubject.next(this.leerUsuario());
  }

  cerrar(): void {
    localStorage.removeItem(CLAVE_ACCESS);
    localStorage.removeItem(CLAVE_REFRESH);
    this.usuarioSubject.next(null);
  }

  accessTokenPorVencer(margenSegundos = 30): boolean {
    const usuario = this.usuario;
    return usuario !== null && usuario.expiraEn - Date.now() < margenSegundos * 1000;
  }

  tieneRol(rol: string): boolean {
    return this.usuario?.roles.includes(rol) ?? false;
  }

  tieneAlgunRol(roles: readonly string[]): boolean {
    return roles.some(rol => this.tieneRol(rol));
  }

  private leerUsuario(): UsuarioSesion | null {
    const token = localStorage.getItem(CLAVE_ACCESS);
    if (!token) {
      return null;
    }

    try {
      const carga = JSON.parse(this.decodificarBase64Url(token.split('.')[1]));
      const roles = carga.role === undefined ? [] : Array.isArray(carga.role) ? carga.role : [carga.role];
      return {
        id: carga.sub,
        email: carga.email,
        nombre: carga.given_name ?? '',
        apellido: carga.family_name ?? '',
        roles,
        expiraEn: carga.exp * 1000
      };
    } catch {
      return null;
    }
  }

  // El JWT viaja en Base64Url y en UTF-8 (nombres con tildes): atob solo no basta.
  private decodificarBase64Url(texto: string): string {
    const base64 = texto.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(texto.length / 4) * 4, '=');
    const bytes = Uint8Array.from(atob(base64), caracter => caracter.charCodeAt(0));
    return new TextDecoder().decode(bytes);
  }
}
