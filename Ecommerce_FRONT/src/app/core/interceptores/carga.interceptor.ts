import { HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, finalize } from 'rxjs';
import { CargaService } from '../servicios/carga.service';
import { SIN_BARRA_DE_CARGA } from './contexto-http';

@Injectable()
export class CargaInterceptor implements HttpInterceptor {
  constructor(private carga: CargaService) {}

  intercept(peticion: HttpRequest<unknown>, siguiente: HttpHandler): Observable<HttpEvent<unknown>> {
    if (peticion.context.get(SIN_BARRA_DE_CARGA)) {
      return siguiente.handle(peticion);
    }
    this.carga.iniciar();
    return siguiente.handle(peticion).pipe(finalize(() => this.carga.terminar()));
  }
}
