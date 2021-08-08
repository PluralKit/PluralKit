import React from 'react';
import * as BS from 'react-bootstrap';
import { useParams } from "react-router-dom";
import ProfilePage from '../Components/Public/ProfilePage.js'

export default function MemberPages(props) {
    const { memberID } = useParams();
    
    const memberpages = props.members.filter((member) => member.id === memberID)
    const memberpage = memberpages.map((member) => <ProfilePage key={member.id} member={member} list={true}/>)
    const noMatch = memberpages.length === 0;

    if (noMatch) return (
        <BS.Alert variant="danger">This system does not have a member with the ID '{memberID}', or the member's visibility is set to private.</BS.Alert>
    )

    return (
        <>
            {memberpage}
        </>
    )
}