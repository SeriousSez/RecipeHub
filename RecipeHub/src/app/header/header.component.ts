import { Component, OnInit,OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';

import { UserService } from '../shared/services/user.service';

@Component({
    selector: 'app-header',
    templateUrl: './header.component.html',
    styleUrls: ['./header.component.scss'],
    standalone: false
})
export class HeaderComponent implements OnInit,OnDestroy {

  username: string = '';
  email: string = '';
  isAuthenticated: boolean = false;
  isAdmin: boolean = false;
  subscription?: Subscription;
  subscription2?: Subscription;

  constructor(private userService: UserService, private router: Router) { }

  logout() {
    this.userService.logout();
    this.router.navigate(['/']);
  }

  ngOnInit() {
    this.username = this.userService.getUserName();
    this.email = this.userService.getEmail();
    this.subscription = this.userService.authStatus$.subscribe(result => this.isAuthenticated = result);
    this.subscription2 = this.userService.adminStatus$.subscribe(result => this.isAdmin = result);
  }

  ngOnDestroy() {
    // prevent memory leak when component is destroyed
    this.subscription?.unsubscribe();
  }
}