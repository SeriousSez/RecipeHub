import { Directive, ElementRef, Renderer2, OnInit } from "@angular/core";

@Directive({
    selector: '[tmFocus]',
    standalone: false
})

export class Focus implements OnInit {
    constructor(private el: ElementRef, private renderer: Renderer2) {
        // focus won't work at construction time - too early
    }

    ngOnInit() {
        // this.renderer.createElement(this.el.nativeElement, 'focus');
    }
}