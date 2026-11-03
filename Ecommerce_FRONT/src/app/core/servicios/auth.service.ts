import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, finalize, of, tap } from 'rxjs';
import { API } from '../config/api.config';
import { AuthResponse, LoginRequest, RegistroRequest, UsuarioActual } from '../modelos/auth.model';
import { SesionService } from './sesion.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(private http: HttpClient, private sesion: SesionService) {}

  login(datos: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${API.auth}/login`, datos).pipe(
      tap(respuesta => this.sesion.guardar(respuesta))
    );
  }

  registrar(datos: RegistroRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${API.auth}/registro`, datos).pipe(
      tap(respuesta => this.sesion.guardar(respuesta))
    );
  }

  obtenerUsuarioActual(): Observable<UsuarioActual> {
    return this.http.get<UsuarioActual>(`${API.auth}/current`);
  }

  // Revoca el refresh token en el servidor; aunque falle, la sesión local se cierra igual.
  cerrarSesion(): Observable<unknown> {
    const refreshToken = this.sesion.refreshToken;
    if (!refreshToken) {
      this.sesion.cerrar();
      return of(null);
    }

    return this.http.post(`${API.auth}/revoke`, { refreshToken }).pipe(
      catchError(() => of(null)),
      finalize(() => this.sesion.cerrar())
    );
  }
}
