import React from 'react';
import Head from 'next/head';
import Theme2016 from "../../../components/theme2016";
import UserProfile from "../../../components/userProfile";
import UserProfileStore from "../../../components/userProfile/stores/UserProfileStore";
import { getUserInfo } from "../../../services/users";

const UserProfilePage = ({ username, userId, description, ...props }) => {
    const ogTitle = username + "'s Profile" || "Marine";
    const ogUrl = userId ? `https://silrev.biz/users/${userId}/profile` : '';
    const ogDesc = description || 'Join Marine and explore together!';
    
    return (
        <>
            {username && (
                <Head>
                    <title>{ogTitle} - Marine</title>
                    <meta property="og:title" content={ogTitle}/>
                    <meta property="og:url" content={ogUrl}/>
                    <meta property="og:type" content="profile"/>
                    <meta property="og:description" content={ogDesc}/>
                    <meta property="og:image"
                          content={`https://silrev.biz/thumbs/avatar-headshot.ashx?userId=${userId}`}/>
                    <meta name="og:site_name" content="Marine"/>
                    <meta name="theme-color" content="#E2231A"/>
                    <script src="/js/3d/three-r137/three.js"/>
                    <script src="/js/3d/three-r137/MTLLoaderr.js"/>
                    <script src="/js/3d/three-r137/OBJLoaderr.js"/>
                    <script src="/js/3d/three-r137/RobloxOrbitControls.js"/>
                    <script src="/js/3d/tween.js"/>
                </Head>
            )}
            <UserProfileStore.Provider>
                <Theme2016>
                    <UserProfile userId={userId}/>
                </Theme2016>
            </UserProfileStore.Provider>
        </>
    );
};

export async function getServerSideProps(context) {
    const { userId } = context.query;
    // we will get the username, desc
    try {
        const info = await getUserInfo({ userId });
        const username = info.name || "Marine";
        const description = info.description || "No description available";
        return {
            props: {
                username,
                description,
                userId
            }
        };
    } catch (error) {
        console.error("Error fetching user info in profile.js" + error);
        return {
            props: {
                username: "Marine",
                description: "Join Marine and explore together!",
                userId
            }
        };
    }
}

export default UserProfilePage;
