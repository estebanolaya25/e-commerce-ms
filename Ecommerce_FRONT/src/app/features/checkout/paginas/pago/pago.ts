import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription, interval, switchMap, takeUntil, takeWhile, timer } from 'rxjs';
import { Pedido } from '../../../../core/modelos/pedido.model';
import { PedidosService } from '../../../../core/servicios/pedidos.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';
import { fechaUtc } from '../../../../core/utilidades/fechas';

type Vista = 'cargando' | 'formulario' | 'procesando' | 'demorado' | 'cerrado';

const ESTADOS_PAGADOS = ['Pagado', 'EnPreparacion', 'Enviado', 'Entregado'];

// Algoritmo de Luhn: el mismo que valida Pagos (y los bancos) para detectar números mal digitados.
function luhn(control: AbstractControl): ValidationErrors | null {
  const digitos = String(control.value ?? '').replace(/\D/g, '');
  if (digitos.length < 13 || digitos.length > 19) {
    return { luhn: true };
  }
  let suma = 0;
  for (let i = 0; i < digitos.length; i++) {
    let d = Number(digitos[digitos.length - 1 - i]);
    if (i % 2 === 1) {
      d *= 2;
      if (d > 9) d -= 9;
    }
    suma += d;
  }
  return suma % 10 === 0 ? null : { luhn: true };
}

function noVencida(grupo: AbstractControl): ValidationErrors | null {
  const mes = Number(grupo.get('mesVencimiento')?.value);
  const anio = Number(grupo.get('anioVencimiento')?.value);
  const hoy = new Date();
  return anio > hoy.getFullYear() || (anio === hoy.getFullYear() && mes >= hoy.getMonth() + 1) ? null : { vencida: true };
}

@Component({
  selector: 'app-pago',
  standalone: false,
  templateUrl: './pago.html',
  styleUrl: './pago.scss'
})
export class Pago implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private sondeo?: Subscription;

  readonly formulario;
  pedido: Pedido | null = null;
  vista: Vista = 'cargando';
  enviando = false;
  rechazo: string | null = null;
  error: string | null = null;
  restante = '';

  readonly meses = Array.from({ length: 12 }, (_, i) => i + 1);
  readonly anios = Array.from({ length: 11 }, (_, i) => new Date().getFullYear() + i);

  readonly tarjetasPrueba = [
    { numero: '4111 1111 1111 1111', resultado: 'Siempre aprobada', clase: 'aprobada' },
    { numero: '4000 0000 0000 0002', resultado: 'Siempre rechazada', clase: 'rechazada' },
    { numero: '5555 5555 5555 4444', resultado: 'Aleatoria (80 %)', clase: 'aleatoria' }
  ];

  constructor(
    fb: FormBuilder,
    private ruta: ActivatedRoute,
    private router: Router,
    private pedidosService: PedidosService
  ) {
    this.formulario = fb.nonNullable.group(
      {
        numero: ['', [Validators.required, luhn]],
        titular: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(100)]],
        mesVencimiento: [12, Validators.required],
        anioVencimiento: [new Date().getFullYear() + 2, Validators.required],
        cvv: ['', [Validators.required, Validators.pattern(/^[0-9]{3,4}$/)]]
      },
      { validators: noVencida }
    );
  }

  ngOnInit(): void {
    const pedidoId = this.ruta.snapshot.paramMap.get('pedidoId')!;
    this.pedidosService.obtener(pedidoId).subscribe({
      next: pedido => this.mostrar(pedido),
      error: error => {
        this.error = mensajeDeError(error, 'No se encontró el pedido.');
        this.vista = 'cerrado';
      }
    });

    interval(1000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.actualizarCuentaRegresiva());
  }

  get marca(): string | null {
    const numero = this.formulario.controls.numero.value.replace(/\D/g, '');
    if (numero.startsWith('4')) return 'Visa';
    if (/^(5[1-5]|2[2-7])/.test(numero)) return 'Mastercard';
    if (/^3[47]/.test(numero)) return 'Amex';
    return null;
  }

  invalido(campo: 'numero' | 'titular' | 'cvv'): boolean {
    const control = this.formulario.controls[campo];
    return control.invalid && (control.dirty || control.touched);
  }

  get vencida(): boolean {
    return this.formulario.hasError('vencida') && this.formulario.controls.anioVencimiento.touched;
  }

  // Agrupa el número de a 4 dígitos mientras se escribe.
  formatearNumero(evento: Event): void {
    const campo = evento.target as HTMLInputElement;
    const digitos = campo.value.replace(/\D/g, '').slice(0, 19);
    const formateado = digitos.replace(/(.{4})/g, '$1 ').trim();
    this.formulario.controls.numero.setValue(formateado);
  }

  usarTarjeta(numero: string): void {
    this.formulario.patchValue({ numero, titular: this.formulario.controls.titular.value || 'CLIENTE PRUEBA', cvv: '123' });
    this.formulario.markAllAsTouched();
  }

  pagar(): void {
    if (this.formulario.invalid || !this.pedido) {
      this.formulario.markAllAsTouched();
      return;
    }

    const valores = this.formulario.getRawValue();
    this.enviando = true;
    this.error = null;
    this.pedidosService
      .pagar(this.pedido.pedidoId, { ...valores, numero: valores.numero.replace(/\s/g, ''), titular: valores.titular.trim() })
      .subscribe({
        next: () => {
          this.enviando = false;
          this.rechazo = null;
          this.formulario.controls.cvv.reset('');
          this.seguirPago();
        },
        error: error => {
          this.enviando = false;
          this.error = mensajeDeError(error, 'No se pudo iniciar el pago.');
        }
      });
  }

  private mostrar(pedido: Pedido): void {
    this.pedido = pedido;
    if (ESTADOS_PAGADOS.includes(pedido.estado)) {
      this.router.navigate(['/checkout/confirmacion', pedido.pedidoId], { replaceUrl: true });
    } else if (pedido.estado === 'PagoEnProceso') {
      this.seguirPago();
    } else if (pedido.estado === 'PendientePago') {
      this.vista = 'formulario';
      this.rechazo = pedido.ultimoErrorPago;
    } else {
      this.vista = 'cerrado';
    }
    this.actualizarCuentaRegresiva();
  }

  // El pago es asíncrono: se consulta el pedido cada 2 segundos hasta que deje de estar "PagoEnProceso".
  private seguirPago(): void {
    this.vista = 'procesando';
    this.sondeo?.unsubscribe();
    this.sondeo = timer(1500, 2000)
      .pipe(
        switchMap(() => this.pedidosService.obtener(this.pedido!.pedidoId, true)),
        takeWhile(pedido => pedido.estado === 'PagoEnProceso', true),
        takeUntil(timer(40000)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: pedido => {
          this.pedido = pedido;
          if (pedido.estado !== 'PagoEnProceso') {
            this.mostrar(pedido);
          }
        },
        complete: () => {
          if (this.pedido?.estado === 'PagoEnProceso') {
            this.vista = 'demorado';
          }
        }
      });
  }

  private actualizarCuentaRegresiva(): void {
    if (!this.pedido) {
      return;
    }
    const milisegundos = fechaUtc(this.pedido.expiraEn).getTime() - Date.now();
    if (milisegundos <= 0) {
      this.restante = '00:00';
      if (this.vista === 'formulario') {
        this.vista = 'cerrado';
      }
      return;
    }
    const minutos = Math.floor(milisegundos / 60000);
    const segundos = Math.floor((milisegundos % 60000) / 1000);
    this.restante = `${String(minutos).padStart(2, '0')}:${String(segundos).padStart(2, '0')}`;
  }
}
