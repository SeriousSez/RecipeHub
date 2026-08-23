import { Pipe, SecurityContext } from "@angular/core";
import { DomSanitizer } from "@angular/platform-browser";

@Pipe({
  name: 'safeHtml',
  standalone: false
})
export class SafeService {
  constructor(private sanitizer: DomSanitizer) { }

  transform(html: string) {
    return this.sanitizer.sanitize(SecurityContext.HTML, html) ?? '';
  }
}