import React, { useEffect } from 'react';
import * as BS from 'react-bootstrap';
import { useParams } from "react-router-dom";
import MemberPage from './MemberPage.js'

export default function MemberPages(props) {
    const { memberID } = useParams();
    
    
    const memberpages = props.members.filter((member) => member.id === memberID)
    const memberpage = memberpages.map((member) => <MemberPage key={member.id} member={member}/>)
    const noMatch = memberpages.length === 0;

    useEffect (() => { 
        if (memberpages.length === 0) {
         }
    }, [memberpages])

    if (noMatch) return (
        <BS.Alert variant="danger">You do not have a member with the ID '{memberID}' in your system. Please check the ID again.</BS.Alert>
    )

    return (
        <>
            {memberpage}
        </>
    )
}