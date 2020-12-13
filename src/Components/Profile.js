import * as BS from 'react-bootstrap';
import { FaStar } from "react-icons/fa";

export default function Profile () {
    return (
        <BS.Card>
            <BS.Card.Header>
            <BS.Card.Title><FaStar className="mr-3" />Profile</BS.Card.Title>
            </BS.Card.Header>
            <BS.Card.Body>
                WIP. Public profiles coming soon here!
            </BS.Card.Body>
        </BS.Card>
    )
}