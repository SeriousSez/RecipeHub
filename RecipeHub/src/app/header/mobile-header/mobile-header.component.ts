import { Component, ElementRef, HostBinding, HostListener, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { UserService } from 'src/app/shared/services/user.service';

@Component({
    selector: 'app-mobile-header',
    templateUrl: './mobile-header.component.html',
    styleUrls: ['./mobile-header.component.scss'],
    standalone: false
})
export class MobileHeaderComponent implements OnInit {
  @HostBinding('class.navbar-opened') navbarOpened = false;
  @HostBinding('class.second-navbar-opened') secondNavbarOpened = false;

  @HostListener('window:scroll', []) onWindowScroll(){
    var offset = window.pageYOffset || document.documentElement.scrollTop || document.body.scrollTop || 0;
    if(offset > 10){
      this.isFixedNavbar = true;
    }else{
      this.isFixedNavbar = false;
    }
  }

  @HostListener('document:click', ['$event'])
  clickout(event: any) {
    if(!this.elementRef.nativeElement.contains(event.target)) {
      this.closeNavbar();
    }
  }
  
  isAuthenticated: boolean = false;
  isAdmin: boolean = false;
  subscription?: Subscription;
  subscription2?: Subscription;

  public isFixedNavbar = false;
  public open: boolean = false;

  constructor(private userService: UserService, private router: Router, private elementRef: ElementRef) { }

  ngOnInit(): void {
    this.subscription = this.userService.authStatus$.subscribe(result => this.isAuthenticated = result);
    this.subscription2 = this.userService.adminStatus$.subscribe(result => this.isAdmin = result);
  }

  ngOnDestroy() {
    // prevent memory leak when component is destroyed
    this.subscription?.unsubscribe();
  }

  toggleNavbar(){
    this.navbarOpened = !this.navbarOpened;
    this.toggleBurgerMenu();
  }

  closeNavbar(){
    this.navbarOpened = false;
    
    const menuBtn = document.querySelector('.menu-btn');
    if(menuBtn == null) return;
    if(this.open) menuBtn.classList.remove('open');
  }

  toggleBurgerMenu(){
    const menuBtn = document.querySelector('.menu-btn');
    if(menuBtn == null) return;
    
    if(!this.open) {
      menuBtn.classList.add('open');
      this.open = true;
    } else {
      menuBtn.classList.remove('open');
      this.open = false;
    }
  }

  logout() {
    this.userService.logout();
    this.toggleNavbar();
    this.router.navigate(['/']);
  }

}
