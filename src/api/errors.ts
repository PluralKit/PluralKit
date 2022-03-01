enum ErrorType {
    Unknown = 0,
    InvalidToken = 401,
    NotFound = 404,
    InternalServerError = 500,
}

interface ApiError {
    code: number,
    type: ErrorType,
    message?: string,
    data?: any,
}

export function parse(code: number, data?: any): ApiError {
    var type = ErrorType[ErrorType[code]] ?? ErrorType.Unknown;
    if (code >= 500) type = ErrorType.InternalServerError;

    var err: ApiError = { code, type };

    if (data) {
        var d = data;
        err.message = d.message;
        err.data = d;
    }

    return err;
}
