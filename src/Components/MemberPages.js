import React from 'react';
import { useParams } from "react-router-dom";
import MemberPage from './MemberPage.js'

export default function MemberPages(props) {
    const { memberID } = useParams();
    
    const memberpage = props.members.filter((member) => member.id === memberID).map((member) => <MemberPage key={member.id} member={member}/>)

    return (
        <>
            {memberpage}
        </>
    )
}