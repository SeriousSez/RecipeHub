import { Component, OnInit, ViewEncapsulation } from '@angular/core';

@Component({
  selector: 'app-root',
  encapsulation: ViewEncapsulation.None,
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  standalone: false
})
export class AppComponent implements OnInit {
  title = 'Recipe Hub';

  ngOnInit(): void {
    const preferredTheme = localStorage.getItem('recipehub-theme') ?? 'light';
    document.documentElement.setAttribute('data-theme', preferredTheme);
  }
}
