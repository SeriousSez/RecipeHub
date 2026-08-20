import { ViewportScroller } from '@angular/common';
import { Component, HostListener, OnInit, ViewChild } from '@angular/core';

@Component({
    selector: 'app-mobile-footer',
    templateUrl: './mobile-footer.component.html',
    styleUrls: ['./mobile-footer.component.css'],
    standalone: false
})
export class MobileFooterComponent implements OnInit {
  scrollUp: string = "scroll-up";
  scrollDown: string = "scroll-down";

  @HostListener('window:scroll', ['$event']) onScroll(e: any): void {
    var scrollY = this.viewportScroller.getScrollPosition()[1];
    var footer = document.getElementById("footer");

    if(scrollY > 50){
      footer?.classList.remove(this.scrollUp);
      footer?.classList.add(this.scrollDown);
    }
    if (scrollY < this.lastScroll && footer?.classList.contains(this.scrollDown)) {
      footer?.classList.remove(this.scrollDown);
      footer?.classList.add(this.scrollUp);
    }

    this.lastScroll = scrollY;
  }

  lastScroll: number = 0;

  constructor(private viewportScroller: ViewportScroller) { }

  ngOnInit(): void {
    
  }

}
