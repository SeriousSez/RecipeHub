import { Component, OnInit, ViewEncapsulation } from '@angular/core';
import { LanguageService } from './shared/services/language.service';
import { ThemeService } from './shared/services/theme.service';

@Component({
  selector: 'app-root',
  encapsulation: ViewEncapsulation.None,
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  standalone: false
})
export class AppComponent implements OnInit {
  title = 'Recipe Hub';

  constructor(private languageService: LanguageService, private themeService: ThemeService) { }

  ngOnInit(): void {
    this.themeService.init();
    this.languageService.init();
  }
}
