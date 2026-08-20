import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';

@Component({
    selector: 'app-list-overlay',
    templateUrl: './list.overlay.html',
    styleUrls: ['./list.overlay.css'],
    standalone: false
})
export class ListOverlay implements OnInit {

    @Input() title: string = "Create your account";
    @Input() list: [];

    @Output() finish = new EventEmitter();

    public errors: string = '';
    public isRequesting: boolean = false;
    public submitted: boolean = false;
    
    constructor() { }
  
    ngOnInit() {

    }
}
