import { Component, Input, OnInit } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UserRegistration } from 'src/app/shared/models/user.registration.interface';
import { UserService } from '../shared/services/user.service';

@Component({
    selector: 'app-registration',
    templateUrl: './registration.component.html',
    styleUrls: ['./registration.component.css'],
    standalone: false
})
export class RegistrationComponent implements OnInit {
  
  constructor() { }

  ngOnInit() {

  }
}