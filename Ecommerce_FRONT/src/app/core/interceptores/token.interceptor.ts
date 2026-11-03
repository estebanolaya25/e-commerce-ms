import {
  HttpBackend,
  HttpClient,
  HttpContextToken,
  HttpErrorResponse,
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpRequest
} from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, defer, finalize, map, of, shareReplay, switchMap, throwError } from 'rxjs';
import { API } from '../config/api.config';
import { AuthResponse } from '../modelos/auth.model';
import { SesionService } from '../servicios/sesion.service';

// Marca las peticiones que ya se reintentaron tras renovar el token, para no entrar en un ciclo.
const YA_REINTENTADA = new HttpContextToken<boolean>(() => false);

const RUTAS_SIN_TOKEN = [`${API.auth}/login`, `${API.auth}/registro`, `${API.auth}/refresh`, `${API.auth}/revoke`];

@Injectable()
export class TokenInterceptor implements HttpInterceptor {
  // HttpClient construido sobre HttpBackend: no pasa por los interceptores (evita la dependencia circular
  // HttpClient -> interceptor -> HttpClient y que la propia renovación se intercepte).
  private readonly httpDirecto: HttpClient;
  private renovacionEnCurso: Observable<string> | null = null;

  constructor(backend: HttpBackend, private sesion: SesionService, private router: Router) {
    this.httpDirecto = new HttpClient(backend);
  }

  intercept(peticion: HttpRequest<unknown>, siguiente: HttpHandler): Observable<HttpEvent<unknown>> {
    if (!peticion.url.startsWith('/api/') || RUTAS_SIN_TOKEN.includes(peticion.url) || !this.sesion.estaAutenticada) {
      return siguiente.handle(peticion);
    }

    return defer(() => {
      let tokenUsado: string | null = null;

      // Si el token está por vencer se renueva antes de enviar, para evitar un 401 evitable.
      const token$ = this.sesion.accessTokenPorVencer() ? this.renovarToken() : of(this.sesion.accessToken!);

      return token$.pipe(
        switchMap(token => {
          tokenUsado = token;
          return siguiente.handle(this.conToken(peticion, token));
        }),
        catchError((error: unknown) => {
          if (error instanceof HttpErrorResponse && error.status === 401 && !peticion.context.get(YA_REINTENTADA)) {
            // Si otra petición ya renovó el token mientras esta viajaba con el viejo, basta reintentar
            // con el actual: renovar otra vez gastaría el refresh token sin necesidad.
            const actual = this.sesion.accessToken;
            const nuevoToken$ = actual && actual !== tokenUsado ? of(actual) : this.renovarToken();
            return nuevoToken$.pipe(
              switchMap(token => siguiente.handle(this.conToken(peticion, token).clone({
                context: peticion.context.set(YA_REINTENTADA, true)
              })))
            );
          }
          return throwError(() => error);
        })
      );
    });
  }

  // Si varias peticiones necesitan renovar a la vez, todas esperan la misma renovación (una sola llamada).
  private renovarToken(): Observable<string> {
    if (!this.renovacionEnCurso) {
      this.renovacionEnCurso = this.httpDirecto
        .post<AuthResponse>(`${API.auth}/refresh`, { refreshToken: this.sesion.refreshToken })
        .pipe(
          map(respuesta => {
            this.sesion.guardar(respuesta);
            return respuesta.access_token;
          }),
          catchError(error => {
            this.sesion.cerrar();
            this.router.navigate(['/cuenta/ingresar'], { queryParams: { volver: this.router.url, motivo: 'sesion-expirada' } });
            return throwError(() => error);
          }),
          finalize(() => (this.renovacionEnCurso = null)),
          shareReplay(1)
        );
    }
    return this.renovacionEnCurso;
  }

  private conToken(peticion: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
    return peticion.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  }
}
