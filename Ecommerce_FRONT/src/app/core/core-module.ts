import { NgModule, Optional, SkipSelf } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { CargaInterceptor } from './interceptores/carga.interceptor';
import { ErroresInterceptor } from './interceptores/errores.interceptor';
import { TokenInterceptor } from './interceptores/token.interceptor';

// Servicios, interceptores y guards que viven una sola vez en toda la aplicación.
// Solo AppModule lo importa; si otro módulo lo intenta, falla a propósito.
// Orden de los interceptores: barra de carga -> avisos de error -> token (renovación incluida).
@NgModule({
  declarations: [],
  imports: [CommonModule],
  providers: [
    provideHttpClient(withInterceptorsFromDi()),
    { provide: HTTP_INTERCEPTORS, useClass: CargaInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: ErroresInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: TokenInterceptor, multi: true }
  ]
})
export class CoreModule {
  constructor(@Optional() @SkipSelf() moduloPadre?: CoreModule) {
    if (moduloPadre) {
      throw new Error('CoreModule ya fue cargado. Impórtelo solo en AppModule.');
    }
  }
}
