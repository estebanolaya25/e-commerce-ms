import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { timer } from 'rxjs';
import { TIENDA } from '../../../../core/config/tienda.config';
import { Carrito } from '../../../../core/modelos/carrito.model';
import { DireccionEnvio } from '../../../../core/modelos/pedido.model';
import { CarritoService } from '../../../../core/servicios/carrito.service';
import { PedidosService } from '../../../../core/servicios/pedidos.service';
import { SesionService } from '../../../../core/servicios/sesion.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';

const CLAVE_DIRECCION = 'tienda.ultimaDireccion';

export const DEPARTAMENTOS = [
  'Amazonas', 'Antioquia', 'Arauca', 'Atlántico', 'Bogotá D.C.', 'Bolívar', 'Boyacá', 'Caldas', 'Caquetá', 'Casanare',
  'Cauca', 'Cesar', 'Chocó', 'Córdoba', 'Cundinamarca', 'Guainía', 'Guaviare', 'Huila', 'La Guajira', 'Magdalena', 'Meta',
  'Nariño', 'Norte de Santander', 'Putumayo', 'Quindío', 'Risaralda', 'San Andrés y Providencia', 'Santander', 'Sucre',
  'Tolima', 'Valle del Cauca', 'Vaupés', 'Vichada'
];

@Component({
  selector: 'app-checkout',
  standalone: false,
  templateUrl: './checkout.html',
  styleUrl: './checkout.scss'
})
export class Checkout implements OnInit {
  readonly formulario;
  readonly departamentos = DEPARTAMENTOS;
  carrito: Carrito | null = null;
  cargando = true;
  enviando = false;
  error: string | null = null;
  faltantes: { sku: string; solicitado: number; disponible: number }[] = [];

  // Se genera una vez por visita: si el usuario reintenta o hace doble clic, el backend devuelve el mismo pedido.
  private readonly claveIdempotencia = crypto.randomUUID();

  constructor(
    fb: FormBuilder,
    private carritoService: CarritoService,
    private pedidosService: PedidosService,
    private sesion: SesionService,
    private router: Router
  ) {
    this.formulario = fb.nonNullable.group({
      nombreDestinatario: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(150)]],
      telefono: ['', [Validators.required, Validators.pattern(/^\+?[0-9 ]{7,20}$/)]],
      direccion: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(250)]],
      ciudad: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
      departamento: ['', Validators.required],
      notas: ['', Validators.maxLength(300)]
    });
  }

  ngOnInit(): void {
    this.precargarDireccion();
    this.carritoService.cargar().subscribe({
      next: carrito => {
        if (carrito.items.length === 0) {
          this.router.navigate(['/carrito']);
          return;
        }
        this.carrito = carrito;
        this.cargando = false;
      },
      error: error => {
        this.error = mensajeDeError(error, 'No se pudo cargar el carrito.');
        this.cargando = false;
      }
    });
  }

  get envio(): number {
    return this.carrito && this.carrito.total >= TIENDA.envioGratisDesde ? 0 : TIENDA.costoEnvio;
  }

  invalido(campo: keyof DireccionEnvio): boolean {
    const control = this.formulario.get(campo);
    return !!control && control.invalid && (control.dirty || control.touched);
  }

  opcionesTexto(opciones: Record<string, string>): string {
    return Object.values(opciones).join(' · ');
  }

  confirmar(): void {
    if (this.formulario.invalid) {
      this.formulario.markAllAsTouched();
      return;
    }

    const valores = this.formulario.getRawValue();
    const direccion: DireccionEnvio = {
      ...valores,
      notas: valores.notas.trim() || null
    };

    this.enviando = true;
    this.error = null;
    this.faltantes = [];
    this.pedidosService.crear(direccion, this.claveIdempotencia).subscribe({
      next: pedido => {
        localStorage.setItem(CLAVE_DIRECCION, JSON.stringify(direccion));
        // Carrito se vacía solo al recibir el evento PedidoCreado; se recarga un momento después para la navbar.
        timer(1500).subscribe(() => this.carritoService.cargar().subscribe({ error: () => {} }));
        this.router.navigate(['/checkout/pago', pedido.pedidoId]);
      },
      error: error => {
        this.enviando = false;
        this.faltantes = error?.error?.faltantes ?? [];
        this.error = mensajeDeError(error, 'No se pudo crear el pedido.');
      }
    });
  }

  private precargarDireccion(): void {
    try {
      const guardada = localStorage.getItem(CLAVE_DIRECCION);
      if (guardada) {
        const direccion = JSON.parse(guardada) as DireccionEnvio;
        this.formulario.patchValue({ ...direccion, notas: direccion.notas ?? '' });
        return;
      }
    } catch {
      localStorage.removeItem(CLAVE_DIRECCION);
    }
    const usuario = this.sesion.usuario;
    if (usuario) {
      this.formulario.patchValue({ nombreDestinatario: `${usuario.nombre} ${usuario.apellido}`.trim() });
    }
  }
}
