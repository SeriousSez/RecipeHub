import { Observable, throwError } from 'rxjs';


export abstract class BaseService {

  constructor() { }

  protected handleError(error: any) {
    var applicationError = error.headers.get('Application-Error');

    // either applicationError in header or model error in body
    if (applicationError) {
      return throwError(() => applicationError);
    }

    var modelStateErrors: string = '';
    var serverError = error?.error ?? error;

    if (serverError && typeof serverError === 'object' && !serverError.type) {
      for (var key in serverError) {
        if (serverError[key])
          modelStateErrors += serverError[key] + '\n';
      }
    }

    return throwError(() => modelStateErrors || 'Server error');
  }
}