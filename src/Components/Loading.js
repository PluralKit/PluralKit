import React from 'react';
import  * as BS from 'react-bootstrap'

export default function Loading() {
    return (
        <BS.Container fluid className="text-center"><BS.Spinner animation="border" /></BS.Container>
    )
}