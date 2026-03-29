// this is a proprietary forum implementation.
// if anyone wants to submit a pr for other implementations, they are free to.

import request from "../lib/request"
import { getFullUrl } from "../lib/request";

export const getPostsInSubcategory = ({subCategoryId, limit, cursor}) => {
  return request('GET', getFullUrl('forums', '/v1/sub-category/'+subCategoryId+'/posts?limit='+limit+'&cursor='+cursor)).then(d => d.data);
}

export const getRepliesToThread = ({threadId, limit, cursor}) => {
  return request('GET', getFullUrl('forums', '/v1/threads/'+threadId+'/replies?limit='+limit+'&cursor='+cursor)).then(d => d.data);
}

export const getThreadInfoById = ({threadId}) => {
  return request('GET', getFullUrl('forums', '/v1/threads/' + threadId + '/info')).then(d => d.data);
}

export const getPostById = ({postId}) => {
  return request('GET', getFullUrl('forums', '/v1/posts/' + postId + '/info')).then(d => d.data);
}

export const markAsRead = ({postId}) => {
  return request('POST', getFullUrl('forums', '/v1/posts/' + postId + '/mark-as-read')).then(d => d.data);
}

export const createThread = ({subCategoryId, post, subject}) => {
  return request('POST', getFullUrl('forums', '/v1/sub-category/' + subCategoryId + '/thread'), {
    post,
    subject,
  }).then(d => d.data);
}

export const replyToPost = ({postId, post}) => {
  return request('POST', getFullUrl('forums', '/v1/posts/' + postId + '/reply'), {
    post,
  }).then(d => d.data);
}

export const getSubCategoryInfo = ({subCategoryId}) => {
  return request('GET', getFullUrl('forums', '/v1/sub-category/' + subCategoryId + '/info')).then(d => d.data);
}

export const deletePost = ({postId}) => {
  return request('DELETE', getFullUrl('forums', '/v1/posts/' + postId)).then(d => d.data);
}

export const getPostsByUser = ({userId, offset, limit}) => {
  return request('GET', getFullUrl('forums', '/v1/users/' + userId + '/posts?limit=' + limit +'&cursor=' + offset)).then(d => d.data);
}

const ForumsCategories = [
  {
    id: 1,
    name: 'Marine',
    subCategories: [
      {
        id: 46,
        name: 'All Things Marine',
        description:
          'The area for discussions purely about Marine – the features, the games, and company news.',
      },
      {
        id: 14,
        name: 'Help (Technical Support and Account Issues)',
        description:
          'Seeking account or technical help? Post your questions here.',
      },
      {
        id: 21,
        name: 'Suggestions & Ideas',
        description:
          'Do you have a suggestion and ideas for Marine? Share your feedback here.',
      },
      {
        id: 54,
        name: 'Marine events',
        description:
          'Check here to see the crazy things Marine is doing. Contest information can be found here. Marine is going to be at various Maker Faires and conferences around the globe. Discuss those events here!',
      },
    ],
  },
  {
    id: 8,
    name: 'Club Houses',
    subCategories: [
      {
        id: 13,
        name: 'Marine Talk',
        description:
          'A popular hangout where people talk about various topics.',
      },
      {
        id: 18,
        name: 'Off Topic',
        description:
          'When no other forum makes sense for your post, Off Topic will help it make even less sense.',
      },
      {
        id: 32,
        name: 'Clans & Guilds',
        description:
          'Talk about what’s going on in your Clans, Groups, Companies, and Guilds, and about the Groups feature in general.',
      },
      {
        id: 35,
        name: `Let's Make a Deal`,
        description:
          'A fast paced community dedicated to mastering the Limited Trades and Sales market, and divining the subtleties of the Marine Currency Exchange.',
      },
    ],
  },
];

export const getCategories = () => {
  return ForumsCategories;
}

export const getCategoryBySubCategoryId = (subCategoryId) => {
  for (const item of getCategories()) {
    for (const subcat of item.subCategories) {
      if (subcat.id === subCategoryId)
        return item;
    }
  }
  throw new Error('Invalid subCategoryId');
}

export const getSubcategoryById = (subCategoryId) => {
  for (const item of getCategories()) {
    for (const subcat of item.subCategories) {
      if (subcat.id === subCategoryId)
        return subcat;
    }
  }
  throw new Error('Invalid subCategoryId');
}