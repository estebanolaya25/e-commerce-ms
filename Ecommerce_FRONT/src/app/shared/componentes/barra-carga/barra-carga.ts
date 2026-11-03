import { Component } from '@angular/core';
import { CargaService } from '../../../core/servicios/carga.service';

@Component({
  selector: 'app-barra-carga',
  standalone: false,
  templateUrl: './barra-carga.html',
  styleUrl: './barra-carga.scss'
})
export class BarraCarga {
  readonly activa$;

  constructor(carga: CargaService) {
    this.activa$ = carga.activa$;
  }
}
