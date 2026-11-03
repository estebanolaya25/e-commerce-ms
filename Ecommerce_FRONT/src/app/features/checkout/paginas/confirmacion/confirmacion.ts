import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Pedido } from '../../../../core/modelos/pedido.model';
import { PedidosService } from '../../../../core/servicios/pedidos.service';
import { fechaUtc } from '../../../../core/utilidades/fechas';

@Component({
  selector: 'app-confirmacion',
  standalone: false,
  templateUrl: './confirmacion.html',
  styleUrl: './confirmacion.scss'
})
export class Confirmacion implements OnInit {
  pedido: Pedido | null = null;

  constructor(private ruta: ActivatedRoute, private router: Router, private pedidosService: PedidosService) {}

  ngOnInit(): void {
    const pedidoId = this.ruta.snapshot.paramMap.get('pedidoId')!;
    this.pedidosService.obtener(pedidoId).subscribe({
      next: pedido => {
        if (['PendientePago', 'PagoEnProceso'].includes(pedido.estado)) {
          this.router.navigate(['/checkout/pago', pedidoId], { replaceUrl: true });
          return;
        }
        this.pedido = pedido;
      },
      error: () => this.router.navigate(['/pedidos'])
    });
  }

  get fechaPago(): Date | null {
    const pagado = this.pedido?.historial.find(h => h.estadoNuevo === 'Pagado');
    return pagado ? fechaUtc(pagado.createdAt) : null;
  }

  opcionesTexto(opciones: string | null): string {
    if (!opciones) return '';
    try {
      return Object.values(JSON.parse(opciones) as Record<string, string>).join(' · ');
    } catch {
      return '';
    }
  }
}
