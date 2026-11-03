import { LOCALE_ID, NgModule, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeEsCO from '@angular/common/locales/es-CO';
import { BrowserModule } from '@angular/platform-browser';

import { AppRoutingModule } from './app-routing-module';
import { App } from './app';
import { CoreModule } from './core/core-module';
import { SharedModule } from './shared/shared-module';

// Fechas y números en español de Colombia ("31 oct 2026") en los pipes de Angular.
registerLocaleData(localeEsCO);

@NgModule({
  declarations: [App],
  imports: [BrowserModule, AppRoutingModule, CoreModule, SharedModule],
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    { provide: LOCALE_ID, useValue: 'es-CO' }
  ],
  bootstrap: [App]
})
export class AppModule {}
