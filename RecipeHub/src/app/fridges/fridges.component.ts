import { DatePipe } from '@angular/common';
import { AfterViewInit, Component, OnInit } from '@angular/core';
import { UserService } from '../shared/services/user.service';
import { FridgeService } from './fridge.service';
import { Fridge } from './models/fridge';
import { FridgeGrocery } from './models/Fridge-grocery';

@Component({
    selector: 'app-fridges',
    templateUrl: './fridges.component.html',
    styleUrls: ['./fridges.component.css'],
    standalone: false
})
export class FridgesComponent implements OnInit {
  public loading: boolean = true;
  
  private userId: string;

  public fridges: Fridge[] = [];
  
  constructor(private fridgeService: FridgeService, private userService: UserService, private datepipe: DatePipe) {
    this.userId = this.userService.getUserId();
    console.log(this.userId);
  }

  ngOnInit() {
    this.getFridges();
  }

  getFridges(){
    this.fridgeService.get(this.userId)
      .subscribe((fridges: Fridge[]) => {
        fridges.forEach(fridge => fridge.groceries = []);
        this.fridges = fridges;

        this.loading = false;

        this.getGroceries();
      },
      error => {
        //this.notificationService.printErrorMessage(error);
      });
  }

  getGroceries(){
    this.fridges.forEach(fridge => {
      this.fridgeService.getGroceries(fridge.id)
      .subscribe((groceries: FridgeGrocery[]) => {
        fridge.groceries = groceries;
        console.log(fridge.id);
      },
      error => {
        //this.notificationService.printErrorMessage(error);
      });
    });
  }

  displayDateOnly(date: string){
    return this.datepipe.transform(date, 'dd-MM-yyyy');
  }
}
