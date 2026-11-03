import { Component, Input } from '@angular/core';
import { NOMBRES_ROL } from '../../../../core/config/roles';

@Component({
  selector: 'app-insignia-rol',
  standalone: false,
  templateUrl: './insignia-rol.html',
  styleUrl: './insignia-rol.scss'
})
export class InsigniaRol {
  @Input({ required: true }) rol!: string;

  get texto(): string {
    return NOMBRES_ROL[this.rol] ?? this.rol;
  }
}
