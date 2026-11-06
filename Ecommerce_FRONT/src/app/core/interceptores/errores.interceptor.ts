import { HttpErrorResponse, HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { API } from '../config/api.config';
import { AvisosService } from '../servicios/avisos.service';

// Errores que no dependen de la pantalla (sin conexión, límite de peticiones, servicio caído):
// se avisan una sola vez en un toast aunque fallen varias peticiones a la vez.
// El error se vuelve a lanzar: cada pantalla sigue mostrando su propio mensaje o estado vacío.
@Injectable()
export class ErroresInterceptor implements HttpInterceptor {
  private readonly ultimoAviso = new Map<string, number>();

  // Login, registro y el chat del asistente ya muestran el error en su propia pantalla.
  private readonly rutasConMensajePropio = [`${API.auth}/login`, `${API.auth}/registro`, `${API.asistente}/chat/stream`,
    `${API.asistente}/panel/chat/stream`, `${API.asistente}/panel/descripcion`];

  constructor(private avisos: AvisosService) {}

  intercept(peticion: HttpRequest<unknown>, siguiente: HttpHandler): Observable<HttpEvent<unknown>> {
    return siguiente.handle(peticion).pipe(
      catchError((error: unknown) => {
        if (error instanceof HttpErrorResponse && !this.rutasConMensajePropio.includes(peticion.url)) {
          this.avisar(error.status);
        }
        return throwError(() => error);
      })
    );
  }

  private avisar(estado: number): void {
    if (estado === 0) {
      this.unaVez('sin-conexion', () =>
        this.avisos.error('Sin conexión con el servidor', 'Revise su internet o que el backend esté corriendo.'));
    } else if (estado === 429) {
      this.unaVez('limite', () =>
        this.avisos.error('Demasiadas solicitudes', 'Espere un minuto antes de seguir: el servidor limita las peticiones por usuario.'));
    } else if (estado === 502 || estado === 503 || estado === 504) {
      this.unaVez('servicio', () =>
        this.avisos.error('Servicio no disponible', 'Una parte de la tienda no responde en este momento. Intente en unos segundos.'));
    }
  }

  private unaVez(clave: string, mostrar: () => void): void {
    const ahora = Date.now();
    if (ahora - (this.ultimoAviso.get(clave) ?? 0) > 8000) {
      this.ultimoAviso.set(clave, ahora);
      mostrar();
    }
  }
}
