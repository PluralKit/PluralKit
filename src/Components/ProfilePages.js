import React from 'react';
import { useParams } from "react-router-dom";
import ProfilePage from './ProfilePage.js'

export default function MemberPages(props) {
    const { memberID } = useParams();
    
    const memberpage = props.members.filter((member) => member.id === memberID).map((member) => <ProfilePage key={member.id} member={member}/>)

    return (
        <>
            {memberpage}
        </>
    )
}